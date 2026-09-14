using JobEngine.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Core.DependencyInjection;

public class JobHandlerRegistrationBuilder
{
    private readonly Dictionary<string, Type> _adapterTypes =
        new(StringComparer.OrdinalIgnoreCase);

    internal JobHandlerRegistrationBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public JobHandlerRegistrationBuilder AddHandler<THandler, TPayload>(string jobType)
        where THandler : class, IJobHandler<TPayload>
    {
        if (string.IsNullOrWhiteSpace(jobType))
        {
            throw new ArgumentException("Job type must not be empty.", nameof(jobType));
        }

        if (_adapterTypes.ContainsKey(jobType))
        {
            throw new InvalidOperationException(
                $"A handler is already registered for job type '{jobType}'.");
        }

        Services.AddScoped<IJobHandler<TPayload>, THandler>();
        Services.AddScoped<JobHandlerAdapter<TPayload>>();

        _adapterTypes[jobType] = typeof(JobHandlerAdapter<TPayload>);

        return this;
    }

    internal IReadOnlyDictionary<string, Type> BuildMap() => _adapterTypes;
}