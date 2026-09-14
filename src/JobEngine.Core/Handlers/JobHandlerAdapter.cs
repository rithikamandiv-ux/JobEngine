namespace JobEngine.Core.Handlers;

public class JobHandlerAdapter<TPayload> : IJobHandlerAdapter
{
    private readonly IJobHandler<TPayload> _handler;

    public JobHandlerAdapter(IJobHandler<TPayload> handler)
    {
        _handler = handler;
    }

    public Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JobPayloadSerializer.Deserialize<TPayload>(payloadJson);
        return _handler.HandleAsync(payload, cancellationToken);
    }
}