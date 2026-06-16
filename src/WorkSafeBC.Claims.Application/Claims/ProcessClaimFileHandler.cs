using Microsoft.Extensions.Logging;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Application.Reviews;
using WorkSafeBC.Claims.Application.Validation;

namespace WorkSafeBC.Claims.Application.Claims;

public sealed class ProcessClaimFileHandler(
    IClaimFileParser parser,
    IClaimBusinessValidator validator,
    IClaimMessagePublisher publisher,
    IClaimProcessingLedger ledger,
    IClaimReviewInbox reviewInbox,
    TimeProvider timeProvider,
    ILogger<ProcessClaimFileHandler> logger)
    : ICommandHandler<ProcessClaimFileCommand, ProcessClaimFileResult>
{
    public async Task<ProcessClaimFileResult> HandleAsync(ProcessClaimFileCommand command, CancellationToken cancellationToken)
    {
        if (await ledger.HasProcessedAsync(command.File.FileName, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Skipping already processed file {FileName}.", command.File.FileName);
            return new ProcessClaimFileResult(command.File.FileName, 0, 0, true);
        }

        var parsedClaims = 0;
        var publishedEvents = 0;

        try
        {
            var claims = parser.Parse(command.File);
            parsedClaims = claims.Count;

            foreach (var claim in claims)
            {
                validator.Validate(claim);

                var integrationEvent = new ClaimIngestionEvent(
                    EventId: Guid.NewGuid().ToString("N"),
                    ClaimNumber: claim.ClaimNumber,
                    WorkerId: claim.WorkerId,
                    InjuryDate: claim.InjuryDate,
                    ClaimAmount: claim.ClaimAmount,
                    EmployerNumber: claim.EmployerNumber,
                    Currency: claim.Currency,
                    SourceFileName: claim.SourceFileName,
                    IngestedAtUtc: timeProvider.GetUtcNow());

                await publisher.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
                publishedEvents++;
            }

            await ledger.MarkProcessedAsync(command.File.FileName, claims.Count, cancellationToken).ConfigureAwait(false);

            return new ProcessClaimFileResult(command.File.FileName, parsedClaims, publishedEvents, false);
        }
        catch (Exception exception)
        {
            await ledger.MarkFailedAsync(command.File.FileName, exception.Message, cancellationToken).ConfigureAwait(false);

            var reviewItem = new ClaimReviewItem(
                ReviewId: Guid.NewGuid().ToString("N"),
                FileName: command.File.FileName,
                FileKind: command.File.Kind,
                FailureReason: exception.Message,
                OriginalContent: command.File.Content,
                CreatedAtUtc: timeProvider.GetUtcNow());

            await reviewInbox.QueueAsync(reviewItem, cancellationToken).ConfigureAwait(false);
            await ledger.MarkReviewRequiredAsync(command.File.FileName, exception.Message, reviewItem.ReviewId, cancellationToken).ConfigureAwait(false);

            logger.LogWarning(
                exception,
                "Queued file {FileName} for manual review as review item {ReviewId}.",
                command.File.FileName,
                reviewItem.ReviewId);

            return new ProcessClaimFileResult(
                command.File.FileName,
                parsedClaims,
                publishedEvents,
                false,
                WasQueuedForReview: true,
                FailureReason: exception.Message,
                ReviewItemId: reviewItem.ReviewId);
        }
    }
}
