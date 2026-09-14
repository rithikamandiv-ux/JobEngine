namespace JobEngine.Core.Handlers;

public class JobHandlerRegistry : IJobHandlerRegistry
{
    private readonly Dictionary<string, Type> _adapterTypes;

    public JobHandlerRegistry(IReadOnlyDictionary<string, Type> adapterTypes)
    {
        _adapterTypes = new Dictionary<string, Type>(
            adapterTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetAdapterType(string jobType, out Type adapterType) =>
        _adapterTypes.TryGetValue(jobType, out adapterType!);

    public IReadOnlyCollection<string> RegisteredJobTypes => _adapterTypes.Keys;
}