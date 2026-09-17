using JobEngine.Core.Handlers;

namespace JobEngine.Tests.Handlers;

public class GreetingPayload
{
    public string Name { get; set; } = "";
}

public class FailingPayload
{
    public string Name { get; set; } = "";
}

public class RecordingHandler : IJobHandler<GreetingPayload>
{
    private readonly HandlerCallLog _log;

    public RecordingHandler(HandlerCallLog log)
    {
        _log = log;
    }

    public Task HandleAsync(
        GreetingPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        _log.Calls.Add(payload.Name);
        _log.Contexts.Add(context);
        return Task.CompletedTask;
    }
}

public class ThrowingHandler : IJobHandler<FailingPayload>
{
    public Task HandleAsync(
        FailingPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("handler failed");
}

public class AnotherGreetingHandler : IJobHandler<GreetingPayload>
{
    public Task HandleAsync(
        GreetingPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public class HandlerCallLog
{
    public List<string> Calls { get; } = [];

    public List<JobExecutionContext> Contexts { get; } = [];
}