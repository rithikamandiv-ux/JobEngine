namespace JobEngine.Core.Handlers;

public interface IJobDispatcher
{
    Task DispatchAsync(Job job, CancellationToken cancellationToken);
}