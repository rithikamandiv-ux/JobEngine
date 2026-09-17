using JobEngine.Core.Handlers;
using Microsoft.Extensions.Options;

namespace JobEngine.Core.Retry;

public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;

    public ExponentialBackoffRetryPolicy(
        IOptions<RetryOptions> options,
        TimeProvider timeProvider,
        Random? random = null)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _random = random ?? Random.Shared;
    }

    public RetryDecision Decide(Job job, Exception exception)
    {
        if (IsPermanentFailure(exception))
        {
            return RetryDecision.Fail();
        }

        if (!job.CanRetry)
        {
            return RetryDecision.DeadLetter();
        }

        var delay = CalculateDelay(job.Attempts);
        var retryAt = _timeProvider.GetUtcNow().UtcDateTime.Add(delay);

        return RetryDecision.Retry(retryAt);
    }

    private static bool IsPermanentFailure(Exception exception) =>
        exception is HandlerNotFoundException or InvalidPayloadException;

    private TimeSpan CalculateDelay(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);

        var seconds = _options.BaseDelaySeconds * Math.Pow(2, Math.Min(exponent, 30));

        seconds = Math.Min(seconds, _options.MaxDelaySeconds);

        if (_options.UseJitter)
        {
            seconds = _random.NextDouble() * seconds;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}