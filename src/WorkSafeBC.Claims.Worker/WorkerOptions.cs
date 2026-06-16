namespace WorkSafeBC.Claims.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public bool Enabled { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 15;
}
