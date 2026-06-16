namespace WorkSafeBC.Claims.Application.Abstractions;

public interface IClaimProcessingLedger
{
    Task<bool> HasProcessedAsync(string fileName, CancellationToken cancellationToken);

    Task MarkProcessedAsync(string fileName, int publishedEvents, CancellationToken cancellationToken);

    Task MarkFailedAsync(string fileName, string reason, CancellationToken cancellationToken);

    Task MarkReviewRequiredAsync(string fileName, string reason, string reviewItemId, CancellationToken cancellationToken);
}
