using JobEngine.Core.Recovery;
using JobEngine.Core.Storage;
using Microsoft.Extensions.Options;

namespace JobEngine.Worker;

public class StaleClaimRecoveryService : BackgroundService
{
    private readonly ILogger<StaleClaimRecoveryService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryOptions _options;

    public StaleClaimRecoveryService(
        ILogger<StaleClaimRecoveryService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<RecoveryOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);
        var staleAfter = TimeSpan.FromSeconds(_options.StaleClaimThresholdSeconds);

        _logger.LogInformation(
            "Stale claim recovery started, scanning every {IntervalSeconds}s for claims older than {ThresholdSeconds}s",
            _options.ScanIntervalSeconds, _options.StaleClaimThresholdSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                var released = await ReleaseAsync(staleAfter, stoppingToken);

                if (released > 0)
                {
                    _logger.LogWarning(
                        "Released {Count} stale claim(s) back to Pending", released);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale claim recovery pass failed");
            }
        }

        _logger.LogInformation("Stale claim recovery stopping");
    }

    private async Task<int> ReleaseAsync(TimeSpan staleAfter, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        return await store.ReleaseStaleClaimsAsync(
            staleAfter, _options.BatchSize, stoppingToken);
    }

    private static async Task<bool> SafeWaitAsync(
        PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}