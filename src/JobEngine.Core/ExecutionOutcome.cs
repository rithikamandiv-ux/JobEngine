namespace JobEngine.Core;

public enum ExecutionOutcome
{
    Succeeded,
    Failed,
    Retrying,
    DeadLettered,
    Released
}