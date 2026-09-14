using JobEngine.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace JobEngine.SampleHandlers;

public class DelayedGreetingHandler : IJobHandler<DelayedGreetingPayload>
{
    private readonly ILogger<DelayedGreetingHandler> _logger;

    public DelayedGreetingHandler(ILogger<DelayedGreetingHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        DelayedGreetingPayload payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting greeting, will wait {DelayMs}ms",
            payload.DelayMilliseconds);

        if (payload.DelayMilliseconds > 0)
        {
            await Task.Delay(payload.DelayMilliseconds, cancellationToken);
        }

        _logger.LogInformation("Greeting: {Message}", payload.Message);
    }
}