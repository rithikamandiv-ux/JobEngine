namespace JobEngine.Worker.Api;

public record EnqueueJobRequest(
    string? Type,
    string? PayloadJson,
    int? MaxAttempts,
    DateTime? ScheduledAt);