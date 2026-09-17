namespace JobEngine.Core.Storage;

public interface IJobStore
{
    Task<Job?> TryClaimNextAsync(string workerId, CancellationToken cancellationToken);

    Task MarkSucceededAsync(Job job, CancellationToken cancellationToken);

    Task MarkForRetryAsync(
        Job job, DateTime retryAt, Exception exception, CancellationToken cancellationToken);
    
    Task<int> ReleaseStaleClaimsAsync(
        TimeSpan staleAfter, int batchSize, CancellationToken cancellationToken);

    Task MarkFailedAsync(Job job, Exception exception, CancellationToken cancellationToken);

    Task MarkDeadLetteredAsync(
        Job job, Exception exception, CancellationToken cancellationToken);

    Task ReleaseClaimAsync(Job job, CancellationToken cancellationToken);
}