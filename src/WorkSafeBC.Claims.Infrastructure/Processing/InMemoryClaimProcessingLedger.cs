using System.Collections.Concurrent;
using WorkSafeBC.Claims.Application.Abstractions;

namespace WorkSafeBC.Claims.Infrastructure.Processing;

public sealed class InMemoryClaimProcessingLedger : IClaimProcessingLedger
{
    private readonly ConcurrentDictionary<string, string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> HasProcessedAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_processedFiles.ContainsKey(fileName));
    }

    public Task MarkProcessedAsync(string fileName, int publishedEvents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _processedFiles[fileName] = $"processed:{publishedEvents}";
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(string fileName, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _processedFiles.TryRemove(fileName, out _);
        return Task.CompletedTask;
    }

    public Task MarkReviewRequiredAsync(string fileName, string reason, string reviewItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _processedFiles[fileName] = $"review:{reviewItemId}";
        return Task.CompletedTask;
    }
}
