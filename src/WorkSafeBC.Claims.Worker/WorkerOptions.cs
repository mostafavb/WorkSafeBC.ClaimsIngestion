namespace WorkSafeBC.Claims.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int PollingIntervalSeconds { get; set; } = 15;
}
