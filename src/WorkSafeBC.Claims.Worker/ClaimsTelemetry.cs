using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkSafeBC.Claims.Worker;

public static class ClaimsTelemetry
{
    public const string ActivitySourceName = "WorkSafeBC.ClaimsIngestion";
    public const string MeterName = "WorkSafeBC.ClaimsIngestion";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
}
