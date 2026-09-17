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

    public Task MarkForRetryAsync(
        Job job,
        DateTime retryAt,
        Exception exception,
        CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Pending;
        job.ScheduledAt = retryAt;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.RunAt = null;
        RecordError(job, exception);

        return UpdateAsync(job, cancellationToken);
    }

    public Task MarkFailedAsync(
        Job job,
        Exception exception,
        CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Failed;
        job.CompletedAt = DateTime.UtcNow;
        RecordError(job, exception);

        return UpdateAsync(job, cancellationToken);
    }

    public Task MarkDeadLetteredAsync(
        Job job,
        Exception exception,
        CancellationToken cancellationToken)
    {
        job.Status = JobStatus.DeadLetter;
        job.CompletedAt = DateTime.UtcNow;
        RecordError(job, exception);

        return UpdateAsync(job, cancellationToken);
    }

    private const int MaxErrorMessageLength = 1000;
    private const int MaxErrorDetailLength = 20000;

    private static void RecordError(Job job, Exception exception)
    {
        job.LastErrorMessage = Truncate(exception.Message, MaxErrorMessageLength);
        job.LastErrorDetail = Truncate(exception.ToString(), MaxErrorDetailLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public Task ReleaseClaimAsync(Job job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Pending;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.RunAt = null;
        return UpdateAsync(job, cancellationToken);
    }
    
    public async Task<int> ReleaseStaleClaimsAsync(
        TimeSpan staleAfter,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.Subtract(staleAfter);

        var released = await _db.Database.SqlQuery<long>(
                $"""
                 UPDATE jobs
                 SET status = 'Pending',
                     claimed_by = NULL,
                     claimed_at = NULL,
                     run_at = NULL,
                     version = version + 1
                 WHERE id IN (
                     SELECT id FROM jobs
                     WHERE status = 'Claimed' AND claimed_at < {threshold}
                     ORDER BY claimed_at
                     FOR UPDATE SKIP LOCKED
                     LIMIT {batchSize}
                 )
                 RETURNING id
                 """)
            .ToListAsync(cancellationToken);

        return released.Count;
    }

    private Task UpdateAsync(Job job, CancellationToken cancellationToken)
    {
        _db.Jobs.Update(job);
        return _db.SaveChangesAsync(cancellationToken);
    }
}