using JobEngine.Core.DependencyInjection;
using JobEngine.Persistence;
using JobEngine.SampleHandlers;
using JobEngine.Worker;
using Microsoft.EntityFrameworkCore;
using JobEngine.Worker.Configuration;
using Microsoft.Extensions.Options;
using JobEngine.Core.Storage;
using JobEngine.Persistence.Storage;
using JobEngine.Core.Retry;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<JobDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("JobEngine"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IJobStore, PostgresJobStore>();

builder.Services.AddJobHandlers(handlers => handlers
    .AddHandler<DelayedGreetingHandler, DelayedGreetingPayload>("delayed-greeting"));

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

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();

builder.Services.AddSingleton(sp =>
    WorkerIdentity.Create(
        sp.GetRequiredService<IOptions<WorkerOptions>>().Value.WorkerName));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();