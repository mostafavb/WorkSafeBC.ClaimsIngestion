namespace WorkSafeBC.Claims.Domain.Entities;

public sealed class InjuryClaim
{
    public InjuryClaim(
        string claimNumber,
        string workerId,
        DateOnly injuryDate,
        decimal claimAmount,
        string employerNumber,
        string currency,
        string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            throw new ArgumentException("Claim number is required.", nameof(claimNumber));
        }

        if (string.IsNullOrWhiteSpace(workerId))
        {
            throw new ArgumentException("Worker id is required.", nameof(workerId));
        }

        ClaimNumber = claimNumber.Trim();
        WorkerId = workerId.Trim();
        InjuryDate = injuryDate;
        ClaimAmount = claimAmount;
        EmployerNumber = employerNumber.Trim();
        Currency = string.IsNullOrWhiteSpace(currency) ? "CAD" : currency.Trim().ToUpperInvariant();
        SourceFileName = sourceFileName.Trim();
    }

    public string ClaimNumber { get; }

    public string WorkerId { get; }

    public DateOnly InjuryDate { get; }

    public decimal ClaimAmount { get; }

    public string EmployerNumber { get; }

    public string Currency { get; }

    public string SourceFileName { get; }
}
