using JobEngine.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobHandlers(
        this IServiceCollection services,
        Action<JobHandlerRegistrationBuilder> configure)
    {
        var builder = new JobHandlerRegistrationBuilder(services);
        configure(builder);

        var map = builder.BuildMap();

        services.AddSingleton<IJobHandlerRegistry>(_ => new JobHandlerRegistry(map));
        
        services.AddScoped<IJobDispatcher, JobDispatcher>();

        return services;
    }
}