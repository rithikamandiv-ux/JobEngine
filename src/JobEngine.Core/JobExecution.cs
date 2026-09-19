namespace JobEngine.Core;

public class JobExecution
{
    public long Id { get; set; }

    public long JobId { get; set; }

    public int Attempt { get; set; }

    public string WorkerId { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ExecutionOutcome? Outcome { get; set; }
    
    public int Generation { get; set; }

    public Job Job { get; set; } = null!;
}