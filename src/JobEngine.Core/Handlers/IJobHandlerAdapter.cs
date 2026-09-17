namespace JobEngine.Core.Handlers;

public interface IJobHandlerAdapter
{
    Task HandleAsync(
        string payloadJson,
        JobExecutionContext context,
        CancellationToken cancellationToken);
}