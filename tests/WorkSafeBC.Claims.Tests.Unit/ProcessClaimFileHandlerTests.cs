using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Application.Reviews;
using WorkSafeBC.Claims.Application.Validation;
using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Tests.Unit;

public sealed class ProcessClaimFileHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldSkip_WhenFileWasAlreadyProcessed()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var reviewInbox = new Mock<IClaimReviewInbox>(MockBehavior.Strict);
        var file = CreateFile();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(parser, validator, publisher, ledger, reviewInbox);

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasSkipped.Should().BeTrue();
        result.WasQueuedForReview.Should().BeFalse();
        parser.VerifyNoOtherCalls();
        validator.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
        reviewInbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_ShouldPublishOneEventPerParsedClaim()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var reviewInbox = new Mock<IClaimReviewInbox>(MockBehavior.Strict);
        var file = CreateFile();
        var claims = new[]
        {
            CreateClaim("CLM-1001"),
            CreateClaim("CLM-1002")
        };
        var publishedEvents = new List<ClaimIngestionEvent>();

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        parser.Setup(x => x.Parse(file)).Returns(claims);
        validator.Setup(x => x.Validate(claims[0]));
        validator.Setup(x => x.Validate(claims[1]));
        publisher.Setup(x => x.PublishAsync(It.IsAny<ClaimIngestionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimIngestionEvent, CancellationToken>((evt, _) => publishedEvents.Add(evt))
            .Returns(Task.CompletedTask);
        ledger.Setup(x => x.MarkProcessedAsync(file.FileName, claims.Length, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = CreateHandler(parser, validator, publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasSkipped.Should().BeFalse();
        result.WasQueuedForReview.Should().BeFalse();
        result.PublishedEvents.Should().Be(2);
        publishedEvents.Should().HaveCount(2);
        publishedEvents.Should().OnlyContain(x => x.SourceFileName == file.FileName);
        publishedEvents.Select(x => x.ClaimNumber).Should().ContainInOrder("CLM-1001", "CLM-1002");
        reviewInbox.Verify(x => x.QueueAsync(It.IsAny<ClaimReviewItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldQueueReview_WhenPublishingFails()
    {
        var parser = new Mock<IClaimFileParser>(MockBehavior.Strict);
        var validator = new Mock<IClaimBusinessValidator>(MockBehavior.Strict);
        var publisher = new Mock<IClaimMessagePublisher>(MockBehavior.Strict);
        var ledger = new Mock<IClaimProcessingLedger>(MockBehavior.Strict);
        var reviewInbox = new Mock<IClaimReviewInbox>(MockBehavior.Strict);
        var file = CreateFile();
        var claim = CreateClaim();
        ClaimReviewItem? queuedItem = null;

        ledger.Setup(x => x.HasProcessedAsync(file.FileName, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        parser.Setup(x => x.Parse(file)).Returns([claim]);
        validator.Setup(x => x.Validate(claim));
        publisher.Setup(x => x.PublishAsync(It.IsAny<ClaimIngestionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        ledger.Setup(x => x.MarkFailedAsync(file.FileName, "broker unavailable", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        reviewInbox.Setup(x => x.QueueAsync(It.IsAny<ClaimReviewItem>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimReviewItem, CancellationToken>((item, _) => queuedItem = item)
            .Returns(Task.CompletedTask);
        ledger.Setup(x => x.MarkReviewRequiredAsync(file.FileName, "broker unavailable", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(parser, validator, publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 13, 13, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasQueuedForReview.Should().BeTrue();
        result.FailureReason.Should().Be("broker unavailable");
        result.ReviewItemId.Should().NotBeNullOrWhiteSpace();
        queuedItem.Should().NotBeNull();
        queuedItem!.FileName.Should().Be(file.FileName);
        queuedItem.FailureReason.Should().Be("broker unavailable");
        queuedItem.FileKind.Should().Be(file.Kind);
        queuedItem.CreatedAtUtc.Should().Be(new DateTimeOffset(2026, 6, 13, 13, 0, 0, TimeSpan.Zero));
        ledger.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ProcessClaimFileHandler CreateHandler(
        Mock<IClaimFileParser> parser,
        Mock<IClaimBusinessValidator> validator,
        Mock<IClaimMessagePublisher> publisher,
        Mock<IClaimProcessingLedger> ledger,
        Mock<IClaimReviewInbox> reviewInbox,
        TimeProvider? timeProvider = null)
    {
        return new ProcessClaimFileHandler(
            parser.Object,
            validator.Object,
            publisher.Object,
            ledger.Object,
            reviewInbox.Object,
            timeProvider ?? TimeProvider.System,
            Mock.Of<ILogger<ProcessClaimFileHandler>>());
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
