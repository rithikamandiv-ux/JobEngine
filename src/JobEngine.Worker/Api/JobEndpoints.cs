using JobEngine.Core;
using JobEngine.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using JobEngine.Core.Handlers;

namespace JobEngine.Worker.Api;

public static class JobEndpoints
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static void MapJobEndpoints(this WebApplication app)
    {
        var jobs = app.MapGroup("/api/jobs");

        jobs.MapGet("/", ListJobsAsync);
        jobs.MapGet("/{id:long}", GetJobAsync);
        jobs.MapGet("/{id:long}/executions", GetExecutionsAsync);
        jobs.MapPost("/", EnqueueJobAsync);
        jobs.MapPost("/{id:long}/retry", RetryJobAsync);

        app.MapGet("/api/stats", GetStatsAsync);
    }

    private static async Task<IResult> ListJobsAsync(
        JobDbContext db,
        CancellationToken ct,
        string? status = null,
        string? type = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (page < 1)
        {
            return Results.BadRequest(new { error = "page must be 1 or greater." });
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return Results.BadRequest(
                new { error = $"pageSize must be between 1 and {MaxPageSize}." });
        }

        var query = db.Jobs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsed))
            {
                return Results.BadRequest(new
                {
                    error = $"Unknown status '{status}'.",
                    validValues = Enum.GetNames<JobStatus>()
                });
            }

            query = query.Where(j => j.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(j => j.Type == type);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(j => j.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => JobSummaryResponse.From(j))
            .ToListAsync(ct);

        return Results.Ok(
            new PagedResponse<JobSummaryResponse>(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> GetJobAsync(
        long id,
        JobDbContext db,
        CancellationToken ct)
    {
        var job = await db.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == id, ct);

        return job is null
            ? Results.NotFound(new { error = $"Job {id} not found." })
            : Results.Ok(JobDetailResponse.From(job));
    }

    private static async Task<IResult> GetExecutionsAsync(
        long id,
        JobDbContext db,
        CancellationToken ct)
    {
        var jobExists = await db.Jobs.AnyAsync(j => j.Id == id, ct);

        if (!jobExists)
        {
            return Results.NotFound(new { error = $"Job {id} not found." });
        }

        var executions = await db.Executions
            .AsNoTracking()
            .Where(e => e.JobId == id)
            .OrderBy(e => e.Generation)
            .ThenBy(e => e.Attempt)
            .ToListAsync(ct);

        return Results.Ok(executions.Select(JobExecutionResponse.From).ToList());
    }

    private static async Task<IResult> GetStatsAsync(
        JobDbContext db,
        CancellationToken ct)
    {
        var counts = await db.Jobs
            .AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = Enum.GetValues<JobStatus>()
            .ToDictionary(
                s => s.ToString(),
                s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);

        return Results.Ok(new StatsResponse(byStatus, byStatus.Values.Sum()));
    }
    
        private static async Task<IResult> EnqueueJobAsync(
        EnqueueJobRequest request,
        JobDbContext db,
        IJobHandlerRegistry registry,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return Results.BadRequest(new { error = "type is required." });
        }

        if (!registry.TryGetAdapterType(request.Type, out _))
        {
            return Results.BadRequest(new
            {
                error = $"No handler is registered for job type '{request.Type}'.",
                registeredTypes = registry.RegisteredJobTypes
            });
        }

        var payloadJson = string.IsNullOrWhiteSpace(request.PayloadJson)
            ? "{}"
            : request.PayloadJson;

        if (!IsValidJson(payloadJson))
        {
            return Results.BadRequest(new { error = "payloadJson is not valid JSON." });
        }

        var maxAttempts = request.MaxAttempts ?? 3;

        if (maxAttempts < 1)
        {
            return Results.BadRequest(new { error = "maxAttempts must be 1 or greater." });
        }

        var now = DateTime.UtcNow;
        var scheduledAt = request.ScheduledAt?.ToUniversalTime() ?? now;

        var job = new Job
        {
            Type = request.Type,
            PayloadJson = payloadJson,
            Status = JobStatus.Pending,
            MaxAttempts = maxAttempts,
            CreatedAt = now,
            ScheduledAt = scheduledAt
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/jobs/{job.Id}",
            JobDetailResponse.From(job));
    }

    private static async Task<IResult> RetryJobAsync(
        long id,
        JobDbContext db,
        CancellationToken ct)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, ct);

        if (job is null)
        {
            return Results.NotFound(new { error = $"Job {id} not found." });
        }

        if (job.Status is not (JobStatus.DeadLetter or JobStatus.Failed))
        {
            return Results.Conflict(new
            {
                error = $"Only Failed or DeadLetter jobs can be retried. Job {id} is {job.Status}.",
            });
        }

        job.Generation += 1;
        job.Status = JobStatus.Pending;
        job.Attempts = 0;
        job.ScheduledAt = DateTime.UtcNow;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.RunAt = null;
        job.CompletedAt = null;

        await db.SaveChangesAsync(ct);

        return Results.Ok(JobDetailResponse.From(job));
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}