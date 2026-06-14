using Microsoft.Extensions.Logging;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Application.Validation;

namespace WorkSafeBC.Claims.Application.Claims;

public sealed class ProcessClaimFileHandler(
    IClaimFileParser parser,
    IClaimBusinessValidator validator,
    IClaimMessagePublisher publisher,
    IClaimProcessingLedger ledger,
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

        try
        {
            var claims = parser.Parse(command.File);

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
            }

            await ledger.MarkProcessedAsync(command.File.FileName, claims.Count, cancellationToken).ConfigureAwait(false);

            return new ProcessClaimFileResult(command.File.FileName, claims.Count, claims.Count, false);
        }
        catch (Exception exception)
        {
            await ledger.MarkFailedAsync(command.File.FileName, exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
