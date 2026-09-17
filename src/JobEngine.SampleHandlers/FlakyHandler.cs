using JobEngine.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace JobEngine.SampleHandlers;

public class FlakyHandler : IJobHandler<FlakyPayload>
{
    private readonly ILogger<FlakyHandler> _logger;

    public FlakyHandler(ILogger<FlakyHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        FlakyPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Attempt <= payload.FailUntilAttempt)
        {
            _logger.LogInformation(
                "Job {JobId} failing deliberately on attempt {Attempt} of {MaxAttempts}",
                context.JobId, context.Attempt, context.MaxAttempts);

            throw new InvalidOperationException(
                $"Simulated failure on attempt {context.Attempt}");
        }

        _logger.LogInformation(
            "Job {JobId} succeeded on attempt {Attempt}: {Message}",
            context.JobId, context.Attempt, payload.Message);

        return Task.CompletedTask;
    }
}