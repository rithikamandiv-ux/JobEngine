namespace JobEngine.Core.Retry;

public interface IRetryPolicy
{
    RetryDecision Decide(Job job, Exception exception);
}