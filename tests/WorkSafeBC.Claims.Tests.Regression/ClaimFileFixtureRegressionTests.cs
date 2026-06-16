using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Application.Contracts;
using WorkSafeBC.Claims.Application.Reviews;
using WorkSafeBC.Claims.Application.Validation;
using WorkSafeBC.Claims.Infrastructure.Storage;

namespace WorkSafeBC.Claims.Tests.Regression;

public sealed class ClaimFileFixtureRegressionTests
{
    [Fact]
    public async Task HandleAsync_ShouldProcess_ValidXmlFixture_EndToEnd()
    {
        var file = LoadFixture("valid-batch.xml", ClaimFileKind.Xml);
        var publisher = new RecordingPublisher();
        var ledger = new RecordingLedger();
        var reviewInbox = new RecordingReviewInbox();
        var handler = CreateHandler(publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasSkipped.Should().BeFalse();
        result.WasQueuedForReview.Should().BeFalse();
        result.ParsedClaims.Should().Be(2);
        result.PublishedEvents.Should().Be(2);
        publisher.PublishedEvents.Select(x => x.ClaimNumber).Should().ContainInOrder("CLM-4101", "CLM-4102");
        publisher.PublishedEvents.Should().OnlyContain(x => x.SourceFileName == "valid-batch.xml");
        ledger.ProcessedFiles.Should().ContainSingle(x => x.FileName == "valid-batch.xml" && x.ClaimCount == 2);
        reviewInbox.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldProcess_ValidFlatFileFixture_EndToEnd()
    {
        var file = LoadFixture("valid-batch.flat", ClaimFileKind.FlatFile);
        var publisher = new RecordingPublisher();
        var ledger = new RecordingLedger();
        var reviewInbox = new RecordingReviewInbox();
        var handler = CreateHandler(publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 5, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasQueuedForReview.Should().BeFalse();
        result.ParsedClaims.Should().Be(2);
        result.PublishedEvents.Should().Be(2);
        publisher.PublishedEvents.Select(x => x.ClaimNumber).Should().ContainInOrder("CLM-4201", "CLM-4202");
        reviewInbox.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldQueueReview_ForInvalidBusinessFixture()
    {
        var file = LoadFixture("invalid-negative-amount.xml", ClaimFileKind.Xml);
        var publisher = new RecordingPublisher();
        var ledger = new RecordingLedger();
        var reviewInbox = new RecordingReviewInbox();
        var handler = CreateHandler(publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 10, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasQueuedForReview.Should().BeTrue();
        result.FailureReason.Should().Be("Claim amount must be greater than zero.");
        result.PublishedEvents.Should().Be(0);
        publisher.PublishedEvents.Should().BeEmpty();
        ledger.FailedFiles.Should().ContainSingle(x => x.FileName == "invalid-negative-amount.xml");
        ledger.ReviewRequiredFiles.Should().ContainSingle(x => x.FileName == "invalid-negative-amount.xml");
        reviewInbox.Items.Should().ContainSingle();
        reviewInbox.Items[0].OriginalContent.Should().Be(file.Content);
        reviewInbox.Items[0].FileKind.Should().Be(ClaimFileKind.Xml);
    }

    [Fact]
    public async Task HandleAsync_ShouldQueueReview_ForMalformedXmlFixture()
    {
        var file = LoadFixture("malformed-batch.xml", ClaimFileKind.Xml);
        var publisher = new RecordingPublisher();
        var ledger = new RecordingLedger();
        var reviewInbox = new RecordingReviewInbox();
        var handler = CreateHandler(publisher, ledger, reviewInbox, new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 15, 0, TimeSpan.Zero)));

        var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), CancellationToken.None);

        result.WasQueuedForReview.Should().BeTrue();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        result.PublishedEvents.Should().Be(0);
        publisher.PublishedEvents.Should().BeEmpty();
        ledger.ReviewRequiredFiles.Should().ContainSingle(x => x.FileName == "malformed-batch.xml");
        reviewInbox.Items.Should().ContainSingle();
        reviewInbox.Items[0].OriginalContent.Should().Be(file.Content);
    }

    private static ProcessClaimFileHandler CreateHandler(
        RecordingPublisher publisher,
        RecordingLedger ledger,
        RecordingReviewInbox reviewInbox,
        TimeProvider timeProvider)
    {
        return new ProcessClaimFileHandler(
            new ClaimFileParser(),
            new ClaimBusinessValidator(),
            publisher,
            ledger,
            reviewInbox,
            timeProvider,
            NullLogger<ProcessClaimFileHandler>.Instance);
    }

    private static InboundClaimFile LoadFixture(string fileName, ClaimFileKind kind)
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        return new InboundClaimFile(fileName, content, kind, new DateTimeOffset(2026, 6, 15, 11, 30, 0, TimeSpan.Zero));
    }

    private sealed class RecordingPublisher : IClaimMessagePublisher
    {
        public List<ClaimIngestionEvent> PublishedEvents { get; } = [];

        public Task PublishAsync(ClaimIngestionEvent integrationEvent, CancellationToken cancellationToken)
        {
            PublishedEvents.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLedger : IClaimProcessingLedger
    {
        public List<(string FileName, int ClaimCount)> ProcessedFiles { get; } = [];
        public List<(string FileName, string Reason)> FailedFiles { get; } = [];
        public List<(string FileName, string Reason, string ReviewItemId)> ReviewRequiredFiles { get; } = [];

        public Task<bool> HasProcessedAsync(string fileName, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task MarkProcessedAsync(string fileName, int claimCount, CancellationToken cancellationToken)
        {
            ProcessedFiles.Add((fileName, claimCount));
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string fileName, string failureReason, CancellationToken cancellationToken)
        {
            FailedFiles.Add((fileName, failureReason));
            return Task.CompletedTask;
        }

        public Task MarkReviewRequiredAsync(string fileName, string failureReason, string reviewItemId, CancellationToken cancellationToken)
        {
            ReviewRequiredFiles.Add((fileName, failureReason, reviewItemId));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingReviewInbox : IClaimReviewInbox
    {
        public List<ClaimReviewItem> Items { get; } = [];

        public Task QueueAsync(ClaimReviewItem reviewItem, CancellationToken cancellationToken)
        {
            Items.Add(reviewItem);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
