using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using WorkSafeBC.Claims.Application;
using WorkSafeBC.Claims.Infrastructure;
using WorkSafeBC.Claims.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

builder.AddServiceDefaults();

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ClaimsIngestionWorker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(ClaimsTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(ClaimsTelemetry.MeterName)
        .AddProcessInstrumentation());

await builder.Build().RunAsync();
