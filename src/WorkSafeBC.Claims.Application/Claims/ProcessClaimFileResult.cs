namespace WorkSafeBC.Claims.Application.Claims;

public sealed record ProcessClaimFileResult(
    string FileName,
    int ParsedClaims,
    int PublishedEvents,
    bool WasSkipped,
    bool WasQueuedForReview = false,
    string? FailureReason = null,
    string? ReviewItemId = null);
