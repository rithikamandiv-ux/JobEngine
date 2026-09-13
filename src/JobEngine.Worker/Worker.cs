using JobEngine.Core;
using JobEngine.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();

        var job = new Job
        {
            Type = "send-email",
            PayloadJson = """{"to":"test@example.com","subject":"Hello"}""",
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation("Inserted job with Id {JobId}", job.Id);

        var loaded = await db.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == job.Id, stoppingToken);

        if (loaded is null)
        {
            _logger.LogError("Could not read the job back");
            return;
        }

        _logger.LogInformation(
            "Read back: Type={Type}, Status={Status}, Attempts={Attempts}/{MaxAttempts}, CanRetry={CanRetry}, Version={Version}",
            loaded.Type, loaded.Status, loaded.Attempts, loaded.MaxAttempts, loaded.CanRetry, loaded.Version);
    }
}