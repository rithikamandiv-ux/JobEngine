using JobEngine.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JobEngine.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveHostedServices(services);
            ReplaceDbContext(services);
        });
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        foreach (var descriptor in hostedServices)
        {
            services.Remove(descriptor);
        }
    }

    private void ReplaceDbContext(IServiceCollection services)
    {
        var existing = services
            .Where(d => d.ServiceType == typeof(DbContextOptions<JobDbContext>)
                        || d.ServiceType == typeof(JobDbContext))
            .ToList();

        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<JobDbContext>(options => options
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention());
    }
}