namespace JobEngine.Core;

public enum JobStatus
{
    Pending,
    Claimed,
    Succeeded,
    Failed,
    DeadLetter
}