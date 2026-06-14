namespace WorkSafeBC.Claims.Application.Contracts;

public sealed record ClaimIngestionEvent(
    string EventId,
    string ClaimNumber,
    string WorkerId,
    DateOnly InjuryDate,
    decimal ClaimAmount,
    string EmployerNumber,
    string Currency,
    string SourceFileName,
    DateTimeOffset IngestedAtUtc);
