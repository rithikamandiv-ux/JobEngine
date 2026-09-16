using JobEngine.Core;
using JobEngine.Core.Storage;
using JobEngine.Persistence.Storage;
using JobEngine.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Tests.Persistence;

public class JobClaimingTests : DatabaseTestBase, IClassFixture<PostgresFixture>
{
    public JobClaimingTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private IJobStore CreateStore() => new PostgresJobStore(CreateContext());

    private static Job PendingJob(DateTime? scheduledAt = null) => new()
    {
        Type = "test-job",
        PayloadJson = "{}",
        Status = JobStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ScheduledAt = scheduledAt ?? DateTime.UtcNow
    };

    private async Task SeedJobsAsync(int count)
    {
        await using var db = CreateContext();

        for (var i = 0; i < count; i++)
        {
            db.Jobs.Add(PendingJob());
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TryClaimNext_ReturnsNull_WhenNoJobsPending()
    {
        var store = CreateStore();

        var job = await store.TryClaimNextAsync("worker-1", CancellationToken.None);

        Assert.Null(job);
    }

    [Fact]
    public async Task TryClaimNext_SetsClaimFields()
    {
        await SeedJobsAsync(1);

        var store = CreateStore();
        var job = await store.TryClaimNextAsync("worker-1", CancellationToken.None);

        Assert.NotNull(job);
        Assert.Equal(JobStatus.Claimed, job.Status);
        Assert.Equal("worker-1", job.ClaimedBy);
        Assert.Equal(1, job.Attempts);
        Assert.NotNull(job.ClaimedAt);
        Assert.NotNull(job.RunAt);
    }

    [Fact]
    public async Task TryClaimNext_IgnoresJobsScheduledInTheFuture()
    {
        await using (var db = CreateContext())
        {
            db.Jobs.Add(PendingJob(DateTime.UtcNow.AddMinutes(5)));
            await db.SaveChangesAsync();
        }

        var store = CreateStore();
        var job = await store.TryClaimNextAsync("worker-1", CancellationToken.None);

        Assert.Null(job);
    }

    [Fact]
    public async Task TryClaimNext_NeverReturnsSameJobTwice_UnderConcurrency()
    {
        const int jobCount = 20;
        const int workerCount = 10;

        await SeedJobsAsync(jobCount);

        var claimTasks = Enumerable.Range(0, workerCount)
            .Select(i => ClaimAllAsync($"worker-{i}"))
            .ToList();

        var results = await Task.WhenAll(claimTasks);

        var allClaimedIds = results.SelectMany(ids => ids).ToList();

        Assert.Equal(jobCount, allClaimedIds.Count);
        Assert.Equal(jobCount, allClaimedIds.Distinct().Count());
    }

    private async Task<List<long>> ClaimAllAsync(string workerId)
    {
        var claimed = new List<long>();

        await using var db = CreateContext();
        var store = new PostgresJobStore(db);

        while (true)
        {
            var job = await store.TryClaimNextAsync(workerId, CancellationToken.None);

            if (job is null)
            {
                return claimed;
            }

            claimed.Add(job.Id);
        }
    }
}