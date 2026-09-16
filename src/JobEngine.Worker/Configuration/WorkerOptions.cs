namespace JobEngine.Worker.Configuration;

public class WorkerOptions
{
    public const string SectionName = "Worker";

    public string? WorkerName { get; set; }

    public int PollingIntervalMilliseconds { get; set; } = 1000;
}