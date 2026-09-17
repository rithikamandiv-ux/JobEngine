using JobEngine.Core;
using JobEngine.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Persistence.Storage;

public class PostgresJobStore : IJobStore
{
    private readonly JobDbContext _db;

    public PostgresJobStore(JobDbContext db)
    {
        _db = db;
    }

    public async Task<Job?> TryClaimNextAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        var jobs = await _db.Jobs
            .FromSql(
                $"""
                UPDATE jobs
                SET status = 'Claimed',
                    claimed_by = {workerId},
                    claimed_at = now(),
                    run_at = now(),
                    attempts = attempts + 1,
                    version = version + 1
                WHERE id = (
                    SELECT id FROM jobs
                    WHERE status = 'Pending' AND scheduled_at <= now()
                    ORDER BY scheduled_at
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                )
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return jobs.SingleOrDefault();
    }

    public Task MarkSucceededAsync(Job job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Succeeded;
        job.CompletedAt = DateTime.UtcNow;
        return UpdateAsync(job, cancellationToken);
    }

    public Task MarkFailedAsync(Job job, string error, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Failed;
        job.LastErrorMessage = error;
        job.CompletedAt = DateTime.UtcNow;
        return UpdateAsync(job, cancellationToken);
    }

    public Task ReleaseClaimAsync(Job job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Pending;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.RunAt = null;
        return UpdateAsync(job, cancellationToken);
    }

    private Task UpdateAsync(Job job, CancellationToken cancellationToken)
    {
        _db.Jobs.Update(job);
        return _db.SaveChangesAsync(cancellationToken);
    }
}