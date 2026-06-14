using System.Text.Json;
using FluentAssertions;
using PactNet;
using WorkSafeBC.Claims.Application.Contracts;

namespace WorkSafeBC.Claims.Tests.Contract;

public sealed class PactClaimPublishedEventContractTests
{
    [Fact]
    public void ClaimIngestionEvent_Should_Generate_MessagePact_For_Published_Event()
    {
        var pactDirectory = Path.Combine(Path.GetTempPath(), "claims-pacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pactDirectory);

        var pact = Pact.V4("WorkSafeBC.Claims.Worker", "ClaimsEventBroker", new PactConfig
        {
            PactDir = pactDirectory
        });

        pact.WithMessageInteractions()
            .ExpectsToReceive("a claim ingestion event")
            .WithMetadata("contentType", "application/json")
            .WithMetadata("eventType", "claims.ingested")
            .WithJsonContent(CreateEvent())
            .Verify<ClaimIngestionEvent>(message =>
            {
                message.ClaimNumber.Should().Be("CLM-1001");
                message.WorkerId.Should().Be("WRK-42");
                message.Currency.Should().Be("CAD");
                message.SourceFileName.Should().Be("claims.xml");
            });

        var pactFile = Directory.GetFiles(pactDirectory, "*.json", SearchOption.AllDirectories).Single();
        var pactJson = File.ReadAllText(pactFile);

        pactJson.Should().Contain("a claim ingestion event");
        pactJson.Should().Contain("claims.ingested");
        pactJson.Should().Contain("CLM-1001");
    }

    [Fact]
    public void ClaimIngestionEvent_Should_Serialize_All_Required_Fields()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(CreateEvent()));
        var root = document.RootElement;

        root.TryGetProperty("EventId", out _).Should().BeTrue();
        root.TryGetProperty("ClaimNumber", out _).Should().BeTrue();
        root.TryGetProperty("WorkerId", out _).Should().BeTrue();
        root.TryGetProperty("InjuryDate", out _).Should().BeTrue();
        root.TryGetProperty("ClaimAmount", out _).Should().BeTrue();
        root.TryGetProperty("EmployerNumber", out _).Should().BeTrue();
        root.TryGetProperty("Currency", out _).Should().BeTrue();
        root.TryGetProperty("SourceFileName", out _).Should().BeTrue();
        root.TryGetProperty("IngestedAtUtc", out _).Should().BeTrue();
    }

    [Fact]
    public void ClaimIngestionEvent_Should_Preserve_Stable_Field_Formats()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(CreateEvent()));
        var root = document.RootElement;

        root.GetProperty("InjuryDate").GetString().Should().Be("2026-06-13");
        root.GetProperty("IngestedAtUtc").GetString().Should().Be("2026-06-13T12:00:00+00:00");
        root.GetProperty("ClaimAmount").GetDecimal().Should().Be(1250.50m);
    }

    private static ClaimIngestionEvent CreateEvent() =>
        new(
            EventId: "evt-001",
            ClaimNumber: "CLM-1001",
            WorkerId: "WRK-42",
            InjuryDate: new DateOnly(2026, 6, 13),
            ClaimAmount: 1250.50m,
            EmployerNumber: "EMP-1",
            Currency: "CAD",
            SourceFileName: "claims.xml",
            IngestedAtUtc: new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
}
