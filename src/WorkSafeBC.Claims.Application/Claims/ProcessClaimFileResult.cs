namespace WorkSafeBC.Claims.Application.Claims;

public sealed record ProcessClaimFileResult(
    string FileName,
    int ParsedClaims,
    int PublishedEvents,
    bool WasSkipped);
