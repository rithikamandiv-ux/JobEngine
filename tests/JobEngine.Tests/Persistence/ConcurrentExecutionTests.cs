using JobEngine.Core;
using JobEngine.Core.DependencyInjection;
using JobEngine.Core.Handlers;
using JobEngine.Core.Storage;
using JobEngine.Persistence;
using JobEngine.Persistence.Storage;
using JobEngine.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Tests.Persistence;

public class ConcurrentExecutionTests : DatabaseTestBase, IClassFixture<PostgresFixture>
{
    public ConcurrentExecutionTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private ServiceProvider BuildProvider(ExecutionRecorder recorder)
    {
        var services = new ServiceCollection();

        services.AddSingleton(recorder);

        services.AddDbContext<JobDbContext>(options => options
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IJobStore, PostgresJobStore>();

        services.AddJobHandlers(handlers => handlers
            .AddHandler<CountingHandler, CountingPayload>("counting"));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task SeedJobsAsync(int count, int workMilliseconds)
    {
        await using var db = CreateContext();

        for (var i = 0; i < count; i++)
        {
            db.Jobs.Add(new Job
            {
                Type = "counting",
                PayloadJson = $$"""{"workMilliseconds":{{workMilliseconds}}}""",
                Status = JobStatus.Pending,
                MaxAttempts = 1,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task RunWorkerAsync(
        ServiceProvider provider, string workerId)
    {
        while (true)
        {
            using var scope = provider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

            var job = await store.TryClaimNextAsync(workerId, CancellationToken.None);

            if (job is null)
            {
                return;
            }

            var executionId = await store.StartExecutionAsync(
                job, workerId, CancellationToken.None);

            var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
            await dispatcher.DispatchAsync(job, CancellationToken.None);

            await store.MarkSucceededAsync(job, CancellationToken.None);
            await store.CompleteExecutionAsync(
                executionId, ExecutionOutcome.Succeeded, CancellationToken.None);
        }
    }

    private async Task<ExecutionRecorder> RunRoundAsync(
        int jobCount, int workerCount, int workMilliseconds)
    {
        await SeedJobsAsync(jobCount, workMilliseconds);

        var recorder = new ExecutionRecorder();
        await using var provider = BuildProvider(recorder);

        var workers = Enumerable.Range(0, workerCount)
            .Select(i => RunWorkerAsync(provider, $"worker-{i}"))
            .ToList();

        await Task.WhenAll(workers);

        return recorder;
    }

    [Fact]
    public async Task EveryJob_IsExecutedExactlyOnce()
    {
        var recorder = await RunRoundAsync(
            jobCount: 20, workerCount: 8, workMilliseconds: 5);

        Assert.Empty(recorder.JobsExecutedMoreThanOnce);
        Assert.Equal(20, recorder.All.Count);
        Assert.Equal(20, recorder.TotalExecutions);
    }

    [Fact]
    public async Task ExecutionLog_MatchesActualExecutions()
    {
        var recorder = await RunRoundAsync(
            jobCount: 20, workerCount: 8, workMilliseconds: 5);

        await using var db = CreateContext();

        var logged = await db.Executions
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(recorder.TotalExecutions, logged.Count);

        Assert.All(logged, e =>
        {
            Assert.Equal(ExecutionOutcome.Succeeded, e.Outcome);
            Assert.NotNull(e.CompletedAt);
            Assert.Equal(1, e.Attempt);
        });

        var jobIds = logged.Select(e => e.JobId).ToList();

        Assert.Equal(jobIds.Count, jobIds.Distinct().Count());
    }

    [Fact]
    public async Task EveryJob_IsExecutedExactlyOnce_AcrossRepeatedRounds()
    {
        const int rounds = 20;

        for (var round = 0; round < rounds; round++)
        {
            var recorder = await RunRoundAsync(
                jobCount: 10, workerCount: 6, workMilliseconds: 0);

            Assert.Empty(recorder.JobsExecutedMoreThanOnce);
            Assert.Equal(10, recorder.TotalExecutions);

            await ClearDataAsync();
        }
    }

    [Fact]
    public async Task MoreWorkersThanJobs_StillExecutesEachJobOnce()
    {
        var recorder = await RunRoundAsync(
            jobCount: 3, workerCount: 20, workMilliseconds: 0);

        Assert.Empty(recorder.JobsExecutedMoreThanOnce);
        Assert.Equal(3, recorder.TotalExecutions);
    }
}