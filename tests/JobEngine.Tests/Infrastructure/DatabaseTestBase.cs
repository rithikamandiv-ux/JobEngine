using JobEngine.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobEngine.Tests.Infrastructure;

public abstract class DatabaseTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    protected DatabaseTestBase(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    protected JobDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JobDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await ClearDataAsync();
    }

    private async Task ClearDataAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE job_executions, jobs RESTART IDENTITY;";
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}