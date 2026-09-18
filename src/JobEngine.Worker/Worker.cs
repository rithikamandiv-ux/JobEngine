using JobEngine.Core;
using JobEngine.Core.Handlers;
using JobEngine.Core.Retry;
using JobEngine.Core.Storage;
using JobEngine.Worker.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerIdentity _identity;
    private readonly TimeSpan _pollingInterval;
    private readonly IRetryPolicy _retryPolicy;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        WorkerIdentity identity,
        IRetryPolicy retryPolicy,
        IOptions<WorkerOptions> options)
        
    {
        _retryPolicy = retryPolicy;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _identity = identity;
        _pollingInterval = TimeSpan.FromMilliseconds(
            options.Value.PollingIntervalMilliseconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Worker {WorkerId} started, polling every {IntervalMs}ms",
            _identity.Id, _pollingInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await RunOnceAsync(stoppingToken);

                if (!claimed)
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker loop");
                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopping", _identity.Id);
    }

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        var job = await store.TryClaimNextAsync(_identity.Id, stoppingToken);

        if (job is null)
        {
            return false;
        }

        _logger.LogInformation(
            "Claimed job {JobId} of type {JobType}, attempt {Attempt}/{MaxAttempts}",
            job.Id, job.Type, job.Attempts, job.MaxAttempts);

        await ExecuteJobAsync(scope.ServiceProvider, job, stoppingToken);

        return true;
    }

        private async Task ExecuteJobAsync(
        IServiceProvider provider,
        Job job,
        CancellationToken stoppingToken)
    {
        var store = provider.GetRequiredService<IJobStore>();
        var dispatcher = provider.GetRequiredService<IJobDispatcher>();

        long executionId;

        try
        {
            executionId = await store.StartExecutionAsync(
                job, _identity.Id, stoppingToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Could not record execution start for job {JobId} attempt {Attempt}. " +
                "This may indicate duplicate execution.",
                job.Id, job.Attempts);
            throw;
        }

        try
        {
            await dispatcher.DispatchAsync(job, stoppingToken);

            await store.MarkSucceededAsync(job, CancellationToken.None);
            await store.CompleteExecutionAsync(
                executionId, ExecutionOutcome.Succeeded, CancellationToken.None);

            _logger.LogInformation("Job {JobId} succeeded", job.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await store.ReleaseClaimAsync(job, CancellationToken.None);
            await store.CompleteExecutionAsync(
                executionId, ExecutionOutcome.Released, CancellationToken.None);

            _logger.LogWarning(
                "Job {JobId} was interrupted by shutdown and released back to Pending",
                job.Id);

            throw;
        }
        catch (Exception ex)
        {
            var decision = _retryPolicy.Decide(job, ex);

            switch (decision.Action)
            {
                case RetryAction.Retry:
                    await store.MarkForRetryAsync(
                        job, decision.RetryAt!.Value, ex, CancellationToken.None);
                    await store.CompleteExecutionAsync(
                        executionId, ExecutionOutcome.Retrying, CancellationToken.None);

                    _logger.LogWarning(ex,
                        "Job {JobId} failed on attempt {Attempt}/{MaxAttempts}, retrying at {RetryAt}",
                        job.Id, job.Attempts, job.MaxAttempts, decision.RetryAt);
                    break;

                case RetryAction.Fail:
                    await store.MarkFailedAsync(job, ex, CancellationToken.None);
                    await store.CompleteExecutionAsync(
                        executionId, ExecutionOutcome.Failed, CancellationToken.None);

                    _logger.LogError(ex,
                        "Job {JobId} failed permanently: {Reason}",
                        job.Id, ex.GetType().Name);
                    break;

                case RetryAction.DeadLetter:
                    await store.MarkDeadLetteredAsync(job, ex, CancellationToken.None);
                    await store.CompleteExecutionAsync(
                        executionId, ExecutionOutcome.DeadLettered, CancellationToken.None);

                    _logger.LogError(ex,
                        "Job {JobId} dead-lettered after {Attempts} attempts",
                        job.Id, job.Attempts);
                    break;
            }
        }
    }
}