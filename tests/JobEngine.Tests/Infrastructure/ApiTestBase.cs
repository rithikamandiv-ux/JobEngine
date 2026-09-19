using System.Net.Http.Json;
using JobEngine.Core;
using JobEngine.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Tests.Infrastructure;

public abstract class ApiTestBase : DatabaseTestBase
{
    private ApiFactory? _factory;

    protected ApiTestBase(PostgresFixture fixture) : base(fixture)
    {
    }

    protected HttpClient Client { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _factory = new ApiFactory(ConnectionString);
        Client = _factory.CreateClient();
    }

    public override async Task DisposeAsync()
    {
        Client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected async Task SeedJobsAsync(params Job[] jobs)
    {
        await using var db = CreateContext();
        db.Jobs.AddRange(jobs);
        await db.SaveChangesAsync();
    }

    protected static Job NewJob(
        string type = "delayed-greeting",
        JobStatus status = JobStatus.Pending,
        int attempts = 0,
        int maxAttempts = 3) => new()
    {
        Type = type,
        PayloadJson = """{"message":"test","delayMilliseconds":0}""",
        Status = status,
        Attempts = attempts,
        MaxAttempts = maxAttempts,
        CreatedAt = DateTime.UtcNow,
        ScheduledAt = DateTime.UtcNow
    };
}