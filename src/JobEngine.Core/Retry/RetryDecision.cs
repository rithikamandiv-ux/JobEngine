namespace JobEngine.Core.Retry;

public record RetryDecision(RetryAction Action, DateTime? RetryAt)
{
    public static RetryDecision Fail() => new(RetryAction.Fail, null);

    public static RetryDecision DeadLetter() => new(RetryAction.DeadLetter, null);

    public static RetryDecision Retry(DateTime retryAt) =>
        new(RetryAction.Retry, retryAt);
}