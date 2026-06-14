using System.Text.Json;
using FluentAssertions;
using WorkSafeBC.Claims.Application.Contracts;

namespace WorkSafeBC.Claims.Tests.Contract;

public sealed class ClaimIngestionContractTests
{
    [Fact]
    public void ClaimIngestionEvent_ShouldSerialize_WithExpectedCanonicalShape()
    {
        var integrationEvent = new ClaimIngestionEvent(
            EventId: "evt-001",
            ClaimNumber: "CLM-1001",
            WorkerId: "WRK-42",
            InjuryDate: new DateOnly(2026, 6, 13),
            ClaimAmount: 1250.50m,
            EmployerNumber: "EMP-1",
            Currency: "CAD",
            SourceFileName: "claims.xml",
            IngestedAtUtc: new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(integrationEvent));
        var root = document.RootElement;

        root.GetProperty("ClaimNumber").GetString().Should().Be("CLM-1001");
        root.GetProperty("WorkerId").GetString().Should().Be("WRK-42");
        root.GetProperty("ClaimAmount").GetDecimal().Should().Be(1250.50m);
        root.GetProperty("SourceFileName").GetString().Should().Be("claims.xml");
    }
}
