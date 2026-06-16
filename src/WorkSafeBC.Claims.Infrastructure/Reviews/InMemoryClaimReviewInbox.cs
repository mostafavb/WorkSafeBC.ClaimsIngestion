using System.Collections.Concurrent;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Reviews;

namespace WorkSafeBC.Claims.Infrastructure.Reviews;

public sealed class InMemoryClaimReviewInbox : IClaimReviewInbox
{
    private readonly ConcurrentDictionary<string, ClaimReviewItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task QueueAsync(ClaimReviewItem reviewItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[reviewItem.ReviewId] = reviewItem;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ClaimReviewItem>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<ClaimReviewItem>>(_items.Values.OrderBy(item => item.CreatedAtUtc).ToArray());
    }
}
