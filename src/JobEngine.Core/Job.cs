namespace JobEngine.Core;

public class Job
{
    public long Id { get; set; }

    public string Type { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; } = 3;

    public DateTime CreatedAt { get; set; }

    public DateTime ScheduledAt { get; set; }

    public DateTime? RunAt { get; set; }

    public string? ClaimedBy { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LastErrorMessage { get; set; }
    
    public string? LastErrorDetail { get; set; }

    public int Version { get; set; }
    
    public int Generation { get; set; }

    public bool CanRetry => Attempts < MaxAttempts;
}