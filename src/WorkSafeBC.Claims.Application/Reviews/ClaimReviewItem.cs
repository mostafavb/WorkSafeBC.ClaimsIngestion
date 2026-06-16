using WorkSafeBC.Claims.Application.Claims;

namespace WorkSafeBC.Claims.Application.Reviews;

public sealed record ClaimReviewItem(
    string ReviewId,
    string FileName,
    ClaimFileKind FileKind,
    string FailureReason,
    string OriginalContent,
    DateTimeOffset CreatedAtUtc);
