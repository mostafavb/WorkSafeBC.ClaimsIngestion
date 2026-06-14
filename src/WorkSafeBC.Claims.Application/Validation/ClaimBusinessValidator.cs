using WorkSafeBC.Claims.Domain.Entities;
using WorkSafeBC.Claims.Domain.Exceptions;

namespace WorkSafeBC.Claims.Application.Validation;

public sealed class ClaimBusinessValidator : IClaimBusinessValidator
{
    public void Validate(InjuryClaim claim)
    {
        if (claim.ClaimAmount <= 0)
        {
            throw new DomainRuleViolationException("Claim amount must be greater than zero.");
        }

        if (claim.InjuryDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new DomainRuleViolationException("Injury date cannot be in the future.");
        }

        if (string.IsNullOrWhiteSpace(claim.EmployerNumber))
        {
            throw new DomainRuleViolationException("Employer number is required.");
        }
    }
}
