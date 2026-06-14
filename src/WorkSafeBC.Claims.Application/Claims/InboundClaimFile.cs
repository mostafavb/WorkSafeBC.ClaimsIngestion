namespace WorkSafeBC.Claims.Application.Claims;

public sealed record InboundClaimFile(
    string FileName,
    string Content,
    ClaimFileKind Kind,
    DateTimeOffset ReceivedAtUtc);
