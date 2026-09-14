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

    public Task HandleAsync(GreetingPayload payload, CancellationToken cancellationToken)
    {
        _log.Calls.Add(payload.Name);
        return Task.CompletedTask;
    }
}

public class ThrowingHandler : IJobHandler<FailingPayload>
{
    public Task HandleAsync(FailingPayload payload, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("handler failed");
}

public class HandlerCallLog
{
    public List<string> Calls { get; } = [];
}

public class AnotherGreetingHandler : IJobHandler<GreetingPayload>
{
    public Task HandleAsync(GreetingPayload payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}