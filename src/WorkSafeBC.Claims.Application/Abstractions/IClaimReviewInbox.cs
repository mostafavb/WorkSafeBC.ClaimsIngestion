using WorkSafeBC.Claims.Application.Reviews;

namespace WorkSafeBC.Claims.Application.Abstractions;

public interface IClaimReviewInbox
{
    Task QueueAsync(ClaimReviewItem reviewItem, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClaimReviewItem>> GetPendingAsync(CancellationToken cancellationToken);
}
