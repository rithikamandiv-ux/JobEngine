using JobEngine.Core;
using JobEngine.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Tests.Persistence;

public class ConcurrencyTests : DatabaseTestBase, IClassFixture<PostgresFixture>
{
    public ConcurrencyTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChanges_IncrementsVersion_WhenJobIsModified()
    {
        long jobId;

        await using (var db = CreateContext())
        {
            var job = NewJob();
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            Assert.Equal(0, job.Version);
        }

        await using (var db = CreateContext())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
            job.Status = JobStatus.Claimed;
            await db.SaveChangesAsync();

            Assert.Equal(1, job.Version);
        }
    }

    [Fact]
    public async Task SaveChanges_Throws_WhenTwoContextsUpdateSameJob()
    {
        long jobId;

        await using (var setup = CreateContext())
        {
            var job = NewJob();
            setup.Jobs.Add(job);
            await setup.SaveChangesAsync();
            jobId = job.Id;
        }

        await using var first = CreateContext();
        await using var second = CreateContext();

        var firstJob = await first.Jobs.SingleAsync(j => j.Id == jobId);
        var secondJob = await second.Jobs.SingleAsync(j => j.Id == jobId);

        firstJob.ClaimedBy = "worker-1";
        firstJob.Status = JobStatus.Claimed;
        await first.SaveChangesAsync();

        secondJob.ClaimedBy = "worker-2";
        secondJob.Status = JobStatus.Claimed;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync());
    }

    private static Job NewJob() => new()
    {
        Type = "test-job",
        PayloadJson = "{}",
        CreatedAt = DateTime.UtcNow,
        ScheduledAt = DateTime.UtcNow
    };
}