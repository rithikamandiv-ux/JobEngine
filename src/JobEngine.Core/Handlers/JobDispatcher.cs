using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Core.Handlers;

public class JobDispatcher : IJobDispatcher
{
    private readonly IJobHandlerRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public JobDispatcher(IJobHandlerRegistry registry, IServiceProvider serviceProvider)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
    }

    public Task DispatchAsync(Job job, CancellationToken cancellationToken)
    {
        if (!_registry.TryGetAdapterType(job.Type, out var adapterType))
        {
            throw new HandlerNotFoundException(job.Type);
        }

        var adapter = (IJobHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        return adapter.HandleAsync(job.PayloadJson, cancellationToken);
    }
}