namespace JobEngine.Core.Handlers;

public interface IJobHandlerAdapter
{
    Task HandleAsync(string payloadJson, CancellationToken cancellationToken);
}