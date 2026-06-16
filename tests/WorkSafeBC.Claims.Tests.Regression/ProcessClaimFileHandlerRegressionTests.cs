using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Application.Validation;
using WorkSafeBC.Claims.Domain.Entities;
using WorkSafeBC.Claims.Domain.Exceptions;

namespace WorkSafeBC.Claims.Tests.Regression;

public sealed class ProcessClaimFileHandlerRegressionTests
{
    [Fact]
    public async Task HandleAsync_ShouldSkip_WhenFileWasAlreadyProcessed()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var file = CreateFile();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(parser, validator, publisher, ledger);

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasSkipped.Should().BeTrue();
        result.PublishedEvents.Should().Be(0);
        parser.VerifyNoOtherCalls();
        validator.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
        ledger.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        ledger.Verify(x => x.MarkFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldPublishOneEventPerParsedClaim()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var file = CreateFile();
        var claims = new[]
        {
            CreateClaim("CLM-1001"),
            CreateClaim("CLM-1002")
        };
        var publishedEvents = new List<ClaimIngestionEvent>();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        parser.Setup(x => x.Parse(file)).Returns(claims);
        validator.Setup(x => x.Validate(claims[0]));
        validator.Setup(x => x.Validate(claims[1]));
        publisher.Setup(x => x.PublishAsync(It.IsAny<ClaimIngestionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimIngestionEvent, CancellationToken>((integrationEvent, _) => publishedEvents.Add(integrationEvent))
            .Returns(Task.CompletedTask);
        ledger.Setup(x => x.MarkProcessedAsync(file.FileName, claims.Length, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(
            parser,
            validator,
            publisher,
            ledger,
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasSkipped.Should().BeFalse();
        result.ParsedClaims.Should().Be(2);
        result.PublishedEvents.Should().Be(2);
        publishedEvents.Should().HaveCount(2);
        publishedEvents.Should().OnlyContain(x => x.SourceFileName == file.FileName);
        publishedEvents.Should().OnlyContain(x => x.IngestedAtUtc == new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero));
        publishedEvents.Select(x => x.ClaimNumber).Should().ContainInOrder("CLM-1001", "CLM-1002");
        ledger.Verify(x => x.MarkFailedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldMarkFileFailed_AndStopPublishing_WhenValidationThrows()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var file = CreateFile();
        var invalidClaim = CreateClaim();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        parser.Setup(x => x.Parse(file)).Returns([invalidClaim]);
        validator.Setup(x => x.Validate(invalidClaim))
            .Throws(new DomainRuleViolationException("Claim amount must be greater than zero."));
        ledger.Setup(x => x.MarkFailedAsync(file.FileName, "Claim amount must be greater than zero.", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessClaimFileHandler(
            parser.Object,
            validator.Object,
            publisher.Object,
            ledger.Object,
            TimeProvider.System,
            NullLogger<ProcessClaimFileHandler>.Instance);

        var action = async () => await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        await action.Should().ThrowAsync<DomainRuleViolationException>()
            .WithMessage("Claim amount must be greater than zero.");
        publisher.Verify(x => x.PublishAsync(It.IsAny<ClaimIngestionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        ledger.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        ledger.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ShouldMarkFailed_AndRethrow_WhenPublisherThrows()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var file = CreateFile();
        var claim = CreateClaim();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        parser.Setup(x => x.Parse(file)).Returns([claim]);
        validator.Setup(x => x.Validate(claim));
        publisher.Setup(x => x.PublishAsync(It.IsAny<ClaimIngestionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        ledger.Setup(x => x.MarkFailedAsync(file.FileName, "broker unavailable", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(parser, validator, publisher, ledger);

        var action = async () => await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*broker unavailable*");
        ledger.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ProcessClaimFileHandler CreateHandler(
        Mock<IClaimFileParser> parser,
        Mock<IClaimBusinessValidator> validator,
        Mock<IClaimMessagePublisher> publisher,
        Mock<IClaimProcessingLedger> ledger,
        TimeProvider? timeProvider = null)
    {
        return new ProcessClaimFileHandler(
            parser.Object,
            validator.Object,
            publisher.Object,
            ledger.Object,
            timeProvider ?? TimeProvider.System,
            NullLogger<ProcessClaimFileHandler>.Instance);
    }

    private static InboundClaimFile CreateFile() =>
        new("claims.xml", "<Claims />", ClaimFileKind.Xml, new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    private static InjuryClaim CreateClaim(string claimNumber = "CLM-1001") =>
        new(
            claimNumber,
            "WRK-42",
            new DateOnly(2026, 6, 12),
            1250.50m,
            "EMP-1",
            "CAD",
            "claims.xml");

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}