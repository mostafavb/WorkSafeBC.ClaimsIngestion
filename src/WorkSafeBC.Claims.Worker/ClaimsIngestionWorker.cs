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
                    _successCounter.Add(1);
                    activity?.SetTag("claims.file.claims", result.ParsedClaims);
                    logger.LogInformation(
                        "Processed file {FileName} with {PublishedEvents} published events.",
                        result.FileName,
                        result.PublishedEvents);
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
