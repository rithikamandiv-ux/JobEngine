namespace JobEngine.Core.Handlers;

public record JobExecutionContext(long JobId, int Attempt, int MaxAttempts)
{
    public bool IsFinalAttempt => Attempt >= MaxAttempts;
}