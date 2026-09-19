using JobEngine.Core;

namespace JobEngine.Worker.Api;

public record JobSummaryResponse(
    long Id,
    string Type,
    string Status,
    int Attempts,
    int MaxAttempts,
    int Generation,
    DateTime CreatedAt,
    DateTime ScheduledAt,
    DateTime? CompletedAt,
    string? LastErrorMessage)
{
    public static JobSummaryResponse From(Job job) => new(
        job.Id,
        job.Type,
        job.Status.ToString(),
        job.Attempts,
        job.MaxAttempts,
        job.Generation,
        job.CreatedAt,
        job.ScheduledAt,
        job.CompletedAt,
        job.LastErrorMessage);
}

public record JobDetailResponse(
    long Id,
    string Type,
    string PayloadJson,
    string Status,
    int Attempts,
    int MaxAttempts,
    int Generation,
    DateTime CreatedAt,
    DateTime ScheduledAt,
    DateTime? RunAt,
    string? ClaimedBy,
    DateTime? ClaimedAt,
    DateTime? CompletedAt,
    string? LastErrorMessage,
    string? LastErrorDetail)
{
    public static JobDetailResponse From(Job job) => new(
        job.Id,
        job.Type,
        job.PayloadJson,
        job.Status.ToString(),
        job.Attempts,
        job.MaxAttempts,
        job.Generation,
        job.CreatedAt,
        job.ScheduledAt,
        job.RunAt,
        job.ClaimedBy,
        job.ClaimedAt,
        job.CompletedAt,
        job.LastErrorMessage, 
        job.LastErrorDetail);
}

public record JobExecutionResponse(
    long Id,
    int Generation,
    int Attempt,
    string WorkerId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Outcome,
    double? DurationMilliseconds)
{
    public static JobExecutionResponse From(JobExecution execution) => new(
        execution.Id,
        execution.Generation,
        execution.Attempt,
        execution.WorkerId,
        execution.StartedAt,
        execution.CompletedAt,
        execution.Outcome?.ToString(),
        execution.CompletedAt is null
            ? null
            : (execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds);
}


public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record StatsResponse(
    IReadOnlyDictionary<string, int> CountsByStatus,
    int TotalJobs);