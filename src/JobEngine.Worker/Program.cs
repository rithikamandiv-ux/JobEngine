using JobEngine.Core.DependencyInjection;
using JobEngine.Persistence;
using JobEngine.SampleHandlers;
using JobEngine.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<JobDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("JobEngine"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddJobHandlers(handlers => handlers
    .AddHandler<DelayedGreetingHandler, DelayedGreetingPayload>("delayed-greeting"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();