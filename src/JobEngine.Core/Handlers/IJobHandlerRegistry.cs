namespace JobEngine.Core.Handlers;

public interface IJobHandlerRegistry
{
    bool TryGetAdapterType(string jobType, out Type adapterType);

    IReadOnlyCollection<string> RegisteredJobTypes { get; }
}