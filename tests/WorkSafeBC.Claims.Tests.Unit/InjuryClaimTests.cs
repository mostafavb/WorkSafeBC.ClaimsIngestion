using FluentAssertions;
using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Tests.Unit;

public sealed class InjuryClaimTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenClaimNumberIsMissing()
    {
        var action = () => CreateClaim(claimNumber: " ");

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("claimNumber");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenWorkerIdIsMissing()
    {
        var action = () => CreateClaim(workerId: " ");

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("workerId");
    }

    [Fact]
    public void Constructor_ShouldNormalize_TrimmedValues_AndDefaultCurrency()
    {
        var claim = CreateClaim(
            claimNumber: "  CLM-1001  ",
            workerId: "  WRK-42 ",
            employerNumber: " EMP-7 ",
            currency: " ",
            sourceFileName: " inbound.xml ");

        claim.ClaimNumber.Should().Be("CLM-1001");
        claim.WorkerId.Should().Be("WRK-42");
        claim.EmployerNumber.Should().Be("EMP-7");
        claim.Currency.Should().Be("CAD");
        claim.SourceFileName.Should().Be("inbound.xml");
    }

    [Fact]
    public void Constructor_ShouldUppercaseCurrency_WhenProvided()
    {
        var claim = CreateClaim(currency: " usd ");

        claim.Currency.Should().Be("USD");
    }

    private static InjuryClaim CreateClaim(
        string claimNumber = "CLM-1001",
        string workerId = "WRK-42",
        DateOnly? injuryDate = null,
        decimal claimAmount = 1250.50m,
        string employerNumber = "EMP-1",
        string currency = "CAD",
        string sourceFileName = "claims.xml")
    {
        return new InjuryClaim(
            claimNumber,
            workerId,
            injuryDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            claimAmount,
            employerNumber,
            currency,
            sourceFileName);
    }
}
