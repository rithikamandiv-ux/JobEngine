using JobEngine.Core.DependencyInjection;
using JobEngine.Core.Recovery;
using JobEngine.Core.Retry;
using JobEngine.Core.Storage;
using JobEngine.Persistence;
using JobEngine.Persistence.Storage;
using JobEngine.SampleHandlers;
using JobEngine.Worker;
using JobEngine.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using JobEngine.Worker.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<JobDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("JobEngine"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IJobStore, PostgresJobStore>();

builder.Services.AddJobHandlers(handlers => handlers
    .AddHandler<DelayedGreetingHandler, DelayedGreetingPayload>("delayed-greeting")
    .AddHandler<FlakyHandler, FlakyPayload>("flaky"));

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(o => o.PollingIntervalMilliseconds > 0,
        "PollingIntervalMilliseconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<RetryOptions>()
    .Bind(builder.Configuration.GetSection(RetryOptions.SectionName))
    .Validate(o => o.BaseDelaySeconds > 0, "BaseDelaySeconds must be greater than zero.")
    .Validate(o => o.MaxDelaySeconds >= o.BaseDelaySeconds,
        "MaxDelaySeconds must be at least BaseDelaySeconds.")
    .ValidateOnStart();

builder.Services.AddOptions<RecoveryOptions>()
    .Bind(builder.Configuration.GetSection(RecoveryOptions.SectionName))
    .Validate(o => o.StaleClaimThresholdSeconds > 0,
        "StaleClaimThresholdSeconds must be greater than zero.")
    .Validate(o => o.ScanIntervalSeconds > 0,
        "ScanIntervalSeconds must be greater than zero.")
    .Validate(o => o.BatchSize > 0, "BatchSize must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
    WorkerIdentity.Create(
        sp.GetRequiredService<IOptions<WorkerOptions>>().Value.WorkerName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<StaleClaimRecoveryService>();

var app = builder.Build();

app.MapGet("/health", async (JobDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);

    return canConnect
        ? Results.Ok(new { status = "healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapJobEndpoints();

app.Run();

public partial class Program;