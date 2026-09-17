namespace JobEngine.Core.Handlers;

public interface IJobHandler<in TPayload>
{
    Task HandleAsync(
        TPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken);
}