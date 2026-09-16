using JobEngine.Core;
using JobEngine.Core.Handlers;
using JobEngine.Core.Storage;
using JobEngine.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace JobEngine.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerIdentity _identity;
    private readonly TimeSpan _pollingInterval;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        WorkerIdentity identity,
        IOptions<WorkerOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _identity = identity;
        _pollingInterval = TimeSpan.FromMilliseconds(
            options.Value.PollingIntervalMilliseconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", _identity.Id);

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

        try
        {
            await dispatcher.DispatchAsync(job, stoppingToken);

            await store.MarkSucceededAsync(job, CancellationToken.None);

            _logger.LogInformation("Job {JobId} succeeded", job.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await store.ReleaseClaimAsync(job, CancellationToken.None);

            _logger.LogWarning(
                "Job {JobId} was interrupted by shutdown and released back to Pending",
                job.Id);

            throw;
        }
        catch (Exception ex)
        {
            await store.MarkFailedAsync(job, ex.ToString(), CancellationToken.None);

            _logger.LogError(ex, "Job {JobId} failed", job.Id);
        }
    }
}