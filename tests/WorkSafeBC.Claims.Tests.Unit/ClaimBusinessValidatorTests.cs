using FluentAssertions;
using WorkSafeBC.Claims.Application.Validation;
using WorkSafeBC.Claims.Domain.Entities;
using WorkSafeBC.Claims.Domain.Exceptions;

namespace WorkSafeBC.Claims.Tests.Unit;

public sealed class ClaimBusinessValidatorTests
{
    [Fact]
    public void Validate_ShouldThrow_WhenClaimAmountIsNotPositive()
    {
        var validator = new ClaimBusinessValidator();
        var claim = new InjuryClaim("CLM-1001", "WRK-42", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 0m, "EMP-1", "CAD", "claims.xml");

        var action = () => validator.Validate(claim);

        action.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void Validate_ShouldThrow_WhenInjuryDateIsInTheFuture()
    {
        var validator = new ClaimBusinessValidator();
        var claim = new InjuryClaim("CLM-1001", "WRK-42", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 1250.50m, "EMP-1", "CAD", "claims.xml");

        var action = () => validator.Validate(claim);

        action.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*future*");
    }

    [Fact]
    public void Validate_ShouldThrow_WhenEmployerNumberIsMissing()
    {
        var validator = new ClaimBusinessValidator();
        var claim = new InjuryClaim("CLM-1001", "WRK-42", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 1250.50m, " ", "CAD", "claims.xml");

        var action = () => validator.Validate(claim);

        action.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*Employer number is required*");
    }

    [Fact]
    public void Validate_ShouldPass_WhenClaimIsEligibleForPublishing()
    {
        var validator = new ClaimBusinessValidator();
        var claim = new InjuryClaim("CLM-1001", "WRK-42", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 1250.50m, "EMP-1", "CAD", "claims.xml");

        var action = () => validator.Validate(claim);

        action.Should().NotThrow();
    }
}
