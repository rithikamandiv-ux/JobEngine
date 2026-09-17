using JobEngine.Core;
using JobEngine.Core.Storage;
using JobEngine.Persistence.Storage;
using JobEngine.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Tests.Persistence;

public class StaleClaimRecoveryTests : DatabaseTestBase, IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    public StaleClaimRecoveryTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private IJobStore CreateStore() => new PostgresJobStore(CreateContext());

    private static Job ClaimedJob(DateTime claimedAt, string claimedBy = "dead-worker") => new()
    {
        Type = "test-job",
        PayloadJson = "{}",
        Status = JobStatus.Claimed,
        Attempts = 1,
        MaxAttempts = 3,
        CreatedAt = DateTime.UtcNow,
        ScheduledAt = DateTime.UtcNow,
        ClaimedBy = claimedBy,
        ClaimedAt = claimedAt,
        RunAt = claimedAt
    };

    private async Task<long> SeedAsync(Job job)
    {
        await using var db = CreateContext();
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<Job> LoadAsync(long id)
    {
        await using var db = CreateContext();
        return await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == id);
    }

    [Fact]
    public async Task ReleaseStaleClaims_ReleasesClaimOlderThanThreshold()
    {
        var id = await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-10)));

        var released = await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        Assert.Equal(1, released);

        var job = await LoadAsync(id);

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Null(job.ClaimedBy);
        Assert.Null(job.ClaimedAt);
        Assert.Null(job.RunAt);
    }

    [Fact]
    public async Task ReleaseStaleClaims_LeavesRecentClaimsAlone()
    {
        var id = await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-1)));

        var released = await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        Assert.Equal(0, released);

        var job = await LoadAsync(id);

        Assert.Equal(JobStatus.Claimed, job.Status);
        Assert.Equal("dead-worker", job.ClaimedBy);
    }

    [Fact]
    public async Task ReleaseStaleClaims_DoesNotResetAttempts()
    {
        var id = await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-10)));

        await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        var job = await LoadAsync(id);

        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task ReleaseStaleClaims_IncrementsVersion()
    {
        var id = await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-10)));

        var before = await LoadAsync(id);

        await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        var after = await LoadAsync(id);

        Assert.Equal(before.Version + 1, after.Version);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Succeeded)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.DeadLetter)]
    public async Task ReleaseStaleClaims_OnlyTouchesClaimedJobs(JobStatus status)
    {
        var job = ClaimedJob(DateTime.UtcNow.AddMinutes(-10));
        job.Status = status;

        var id = await SeedAsync(job);

        var released = await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        Assert.Equal(0, released);

        var loaded = await LoadAsync(id);

        Assert.Equal(status, loaded.Status);
    }

    [Fact]
    public async Task ReleaseStaleClaims_RespectsBatchSize()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-10)));
        }

        var released = await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 2, CancellationToken.None);

        Assert.Equal(2, released);
    }

    [Fact]
    public async Task ReleaseStaleClaims_ReturnsZero_WhenNothingStale()
    {
        var released = await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        Assert.Equal(0, released);
    }

    [Fact]
    public async Task ReleasedJob_CanBeClaimedAgain()
    {
        var id = await SeedAsync(ClaimedJob(DateTime.UtcNow.AddMinutes(-10)));

        await CreateStore()
            .ReleaseStaleClaimsAsync(StaleAfter, 100, CancellationToken.None);

        var claimed = await CreateStore()
            .TryClaimNextAsync("live-worker", CancellationToken.None);

        Assert.NotNull(claimed);
        Assert.Equal(id, claimed.Id);
        Assert.Equal("live-worker", claimed.ClaimedBy);
        Assert.Equal(2, claimed.Attempts);
    }
}