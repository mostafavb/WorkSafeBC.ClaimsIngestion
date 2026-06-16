using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;

namespace WorkSafeBC.Claims.Worker;

public sealed class ClaimsIngestionWorker(
    IInboundClaimFileReader fileReader,
    ICommandHandler<ProcessClaimFileCommand, ProcessClaimFileResult> handler,
    IOptions<WorkerOptions> options,
    ILogger<ClaimsIngestionWorker> logger)
    : BackgroundService
{
    private readonly Counter<long> _successCounter = ClaimsTelemetry.Meter.CreateCounter<long>("claims_files_processed_success");
    private readonly Counter<long> _failureCounter = ClaimsTelemetry.Meter.CreateCounter<long>("claims_files_processed_failure");
    private readonly Histogram<double> _latencyHistogram = ClaimsTelemetry.Meter.CreateHistogram<double>("claims_file_processing_latency_ms");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(Math.Max(5, options.Value.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.Value.Enabled)
            {
                logger.LogInformation("Claim ingestion worker is disabled for this deployment slot.");
                await Task.Delay(pollingInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var files = await fileReader.GetPendingFilesAsync(stoppingToken).ConfigureAwait(false);

            foreach (var file in files)
            {
                var startedAt = Stopwatch.GetTimestamp();
                using var activity = ClaimsTelemetry.ActivitySource.StartActivity("claims.process.file", ActivityKind.Consumer);
                activity?.SetTag("claims.file.name", file.FileName);
                activity?.SetTag("claims.file.kind", file.Kind.ToString());

                try
                {
                    var result = await handler.HandleAsync(new ProcessClaimFileCommand(file), stoppingToken).ConfigureAwait(false);

                    if (result.WasSkipped)
                    {
                        activity?.SetTag("claims.file.skipped", true);
                        logger.LogInformation("Skipped file {FileName} because it was already completed.", result.FileName);
                    }
                    else if (result.WasQueuedForReview)
                    {
                        _failureCounter.Add(1);
                        activity?.SetStatus(ActivityStatusCode.Error, result.FailureReason);
                        activity?.SetTag("claims.file.review_required", true);
                        activity?.SetTag("claims.review.id", result.ReviewItemId);
                        logger.LogWarning(
                            "Queued file {FileName} for manual review as review item {ReviewItemId}. Reason: {FailureReason}",
                            result.FileName,
                            result.ReviewItemId,
                            result.FailureReason);
                    }
                    else
                    {
                        _successCounter.Add(1);
                        activity?.SetTag("claims.file.claims", result.ParsedClaims);
                        logger.LogInformation(
                            "Processed file {FileName} with {PublishedEvents} published events.",
                            result.FileName,
                            result.PublishedEvents);
                    }
                }
                catch (Exception exception)
                {
                    _failureCounter.Add(1);
                    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    logger.LogError(exception, "Failed to process file {FileName}.", file.FileName);
                }
                finally
                {
                    _latencyHistogram.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                }
            }

            await Task.Delay(pollingInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
