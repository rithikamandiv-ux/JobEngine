using JobEngine.Core;
using JobEngine.Core.Handlers;
using JobEngine.Core.Retry;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobEngine.Tests.Retry;

public class ExponentialBackoffRetryPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ExponentialBackoffRetryPolicy CreatePolicy(
        int baseDelaySeconds = 2,
        int maxDelaySeconds = 300,
        bool useJitter = false,
        Random? random = null)
    {
        var options = Options.Create(new RetryOptions
        {
            BaseDelaySeconds = baseDelaySeconds,
            MaxDelaySeconds = maxDelaySeconds,
            UseJitter = useJitter
        });

        var time = new FakeTimeProvider(Now);

        return new ExponentialBackoffRetryPolicy(options, time, random);
    }

    private static Job JobWith(int attempts, int maxAttempts = 3) => new()
    {
        Id = 1,
        Type = "test-job",
        PayloadJson = "{}",
        Status = JobStatus.Claimed,
        Attempts = attempts,
        MaxAttempts = maxAttempts,
        CreatedAt = Now.UtcDateTime,
        ScheduledAt = Now.UtcDateTime
    };

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void Decide_DoublesDelay_WithEachAttempt(int attempts, int expectedSeconds)
    {
        var policy = CreatePolicy(maxDelaySeconds: 10_000);

        var decision = policy.Decide(JobWith(attempts, maxAttempts: 100), new Exception());

        Assert.Equal(RetryAction.Retry, decision.Action);
        Assert.Equal(Now.UtcDateTime.AddSeconds(expectedSeconds), decision.RetryAt);
    }

    [Fact]
    public void Decide_CapsDelay_AtMaxDelay()
    {
        var policy = CreatePolicy(baseDelaySeconds: 2, maxDelaySeconds: 10);

        var decision = policy.Decide(JobWith(20, maxAttempts: 100), new Exception());

        Assert.Equal(Now.UtcDateTime.AddSeconds(10), decision.RetryAt);
    }

    [Fact]
    public void Decide_DoesNotOverflow_AtVeryHighAttemptCounts()
    {
        var policy = CreatePolicy(maxDelaySeconds: 300);

        var decision = policy.Decide(JobWith(1000, maxAttempts: 10_000), new Exception());

        Assert.Equal(RetryAction.Retry, decision.Action);
        Assert.Equal(Now.UtcDateTime.AddSeconds(300), decision.RetryAt);
    }

    [Fact]
    public void Decide_ReturnsDeadLetter_WhenAttemptsExhausted()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(JobWith(3, maxAttempts: 3), new Exception());

        Assert.Equal(RetryDecision.DeadLetter(), decision);
    }

    [Fact]
    public void Decide_ReturnsRetry_OnFinalAllowedAttempt()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(JobWith(2, maxAttempts: 3), new Exception());

        Assert.Equal(RetryAction.Retry, decision.Action);
    }

    [Fact]
    public void Decide_ReturnsFail_WhenHandlerNotFound()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(
            JobWith(1), new HandlerNotFoundException("unknown-type"));

        Assert.Equal(RetryDecision.Fail(), decision);
    }

    [Fact]
    public void Decide_ReturnsFail_WhenPayloadInvalid()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(
            JobWith(1), new InvalidPayloadException("bad payload"));

        Assert.Equal(RetryDecision.Fail(), decision);
    }

    [Fact]
    public void Decide_PrefersFail_OverDeadLetter_WhenBothApply()
    {
        var policy = CreatePolicy();

        var decision = policy.Decide(
            JobWith(3, maxAttempts: 3), new HandlerNotFoundException("unknown-type"));

        Assert.Equal(RetryDecision.Fail(), decision);
    }

    [Fact]
    public void Decide_ProducesDelayWithinBounds_WhenJitterEnabled()
    {
        var policy = CreatePolicy(useJitter: true, random: new Random(42));

        for (var i = 0; i < 100; i++)
        {
            var decision = policy.Decide(JobWith(3, maxAttempts: 100), new Exception());

            var delay = decision.RetryAt!.Value - Now.UtcDateTime;

            Assert.InRange(delay.TotalSeconds, 0, 8);
        }
    }

    [Fact]
    public void Decide_ProducesVaryingDelays_WhenJitterEnabled()
    {
        var policy = CreatePolicy(useJitter: true, random: new Random(42));

        var delays = Enumerable.Range(0, 20)
            .Select(_ => policy.Decide(JobWith(3, maxAttempts: 100), new Exception()))
            .Select(d => d.RetryAt!.Value)
            .Distinct()
            .Count();

        Assert.True(delays > 1, "Jitter should produce varying retry times");
    }

    [Fact]
    public void Decide_ProducesIdenticalDelays_WhenJitterDisabled()
    {
        var policy = CreatePolicy(useJitter: false);

        var first = policy.Decide(JobWith(3, maxAttempts: 100), new Exception());
        var second = policy.Decide(JobWith(3, maxAttempts: 100), new Exception());

        Assert.Equal(first, second);
    }
}