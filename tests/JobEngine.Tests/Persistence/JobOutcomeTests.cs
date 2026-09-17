using JobEngine.Core;
using JobEngine.Core.Storage;
using JobEngine.Persistence.Storage;
using JobEngine.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Tests.Persistence;

public class JobOutcomeTests : DatabaseTestBase, IClassFixture<PostgresFixture>
{
    public JobOutcomeTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private IJobStore CreateStore() => new PostgresJobStore(CreateContext());

    private async Task<Job> SeedClaimedJobAsync()
    {
        await using var db = CreateContext();

        var job = new Job
        {
            Type = "test-job",
            PayloadJson = "{}",
            Status = JobStatus.Claimed,
            Attempts = 1,
            MaxAttempts = 3,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow,
            ClaimedBy = "worker-1",
            ClaimedAt = DateTime.UtcNow,
            RunAt = DateTime.UtcNow
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    private async Task<Job> LoadAsync(long id)
    {
        await using var db = CreateContext();
        return await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == id);
    }

    [Fact]
    public async Task MarkForRetry_ReturnsJobToPending_AndClearsClaim()
    {
        var job = await SeedClaimedJobAsync();
        var retryAt = DateTime.UtcNow.AddMinutes(5);

        await CreateStore().MarkForRetryAsync(
            job, retryAt, new InvalidOperationException("boom"), CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal(JobStatus.Pending, loaded.Status);
        Assert.Null(loaded.ClaimedBy);
        Assert.Null(loaded.ClaimedAt);
        Assert.Null(loaded.RunAt);
        Assert.Null(loaded.CompletedAt);
        Assert.Equal(retryAt, loaded.ScheduledAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task MarkForRetry_RecordsBothErrorColumns()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkForRetryAsync(
            job,
            DateTime.UtcNow.AddMinutes(5),
            new InvalidOperationException("boom"),
            CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal("boom", loaded.LastErrorMessage);
        Assert.NotNull(loaded.LastErrorDetail);
        Assert.Contains("InvalidOperationException", loaded.LastErrorDetail);
    }

    [Fact]
    public async Task MarkForRetry_LeavesAttemptsUnchanged()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkForRetryAsync(
            job,
            DateTime.UtcNow.AddMinutes(5),
            new InvalidOperationException("boom"),
            CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal(1, loaded.Attempts);
    }

    [Fact]
    public async Task RetriedJob_IsNotClaimable_UntilScheduledAtPasses()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkForRetryAsync(
            job,
            DateTime.UtcNow.AddMinutes(5),
            new InvalidOperationException("boom"),
            CancellationToken.None);

        var claimed = await CreateStore()
            .TryClaimNextAsync("worker-2", CancellationToken.None);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task RetriedJob_IsClaimable_OnceScheduledAtHasPassed()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkForRetryAsync(
            job,
            DateTime.UtcNow.AddSeconds(-1),
            new InvalidOperationException("boom"),
            CancellationToken.None);

        var claimed = await CreateStore()
            .TryClaimNextAsync("worker-2", CancellationToken.None);

        Assert.NotNull(claimed);
        Assert.Equal(job.Id, claimed.Id);
        Assert.Equal(2, claimed.Attempts);
    }

    [Fact]
    public async Task MarkFailed_SetsTerminalState()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkFailedAsync(
            job, new InvalidOperationException("permanent"), CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal(JobStatus.Failed, loaded.Status);
        Assert.NotNull(loaded.CompletedAt);
        Assert.Equal("permanent", loaded.LastErrorMessage);
        Assert.Equal("worker-1", loaded.ClaimedBy);
    }

    [Fact]
    public async Task MarkDeadLettered_SetsTerminalState()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkDeadLetteredAsync(
            job, new InvalidOperationException("exhausted"), CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal(JobStatus.DeadLetter, loaded.Status);
        Assert.NotNull(loaded.CompletedAt);
        Assert.Equal("exhausted", loaded.LastErrorMessage);
    }

    [Theory]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.DeadLetter)]
    public async Task TerminalJobs_AreNeverClaimed(JobStatus status)
    {
        var job = await SeedClaimedJobAsync();

        if (status == JobStatus.Failed)
        {
            await CreateStore().MarkFailedAsync(
                job, new InvalidOperationException("x"), CancellationToken.None);
        }
        else
        {
            await CreateStore().MarkDeadLetteredAsync(
                job, new InvalidOperationException("x"), CancellationToken.None);
        }

        var claimed = await CreateStore()
            .TryClaimNextAsync("worker-2", CancellationToken.None);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task ErrorMessage_IsTruncated_WhenExtremelyLong()
    {
        var job = await SeedClaimedJobAsync();
        var hugeMessage = new string('x', 50_000);

        await CreateStore().MarkFailedAsync(
            job, new InvalidOperationException(hugeMessage), CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.NotNull(loaded.LastErrorMessage);
        Assert.Equal(1000, loaded.LastErrorMessage.Length);
        Assert.NotNull(loaded.LastErrorDetail);
        Assert.Equal(20_000, loaded.LastErrorDetail.Length);
    }

    [Fact]
    public async Task MarkSucceeded_SetsTerminalState()
    {
        var job = await SeedClaimedJobAsync();

        await CreateStore().MarkSucceededAsync(job, CancellationToken.None);

        var loaded = await LoadAsync(job.Id);

        Assert.Equal(JobStatus.Succeeded, loaded.Status);
        Assert.NotNull(loaded.CompletedAt);
        Assert.Null(loaded.LastErrorMessage);
    }
}