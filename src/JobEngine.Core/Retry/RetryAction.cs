namespace JobEngine.Core.Retry;

public enum RetryAction
{
    Retry,
    Fail,
    DeadLetter
}