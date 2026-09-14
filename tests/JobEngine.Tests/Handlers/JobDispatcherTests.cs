using JobEngine.Core;
using JobEngine.Core.DependencyInjection;
using JobEngine.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Tests.Handlers;

public class JobDispatcherTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped<HandlerCallLog>();

        services.AddJobHandlers(handlers => handlers
            .AddHandler<RecordingHandler, GreetingPayload>("greeting")
            .AddHandler<ThrowingHandler, FailingPayload>("failing-greeting"));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static Job NewJob(string type, string payloadJson) => new()
    {
        Type = type,
        PayloadJson = payloadJson,
        CreatedAt = DateTime.UtcNow,
        ScheduledAt = DateTime.UtcNow
    };

    [Fact]
    public async Task DispatchAsync_InvokesHandler_WithDeserializedPayload()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        var log = scope.ServiceProvider.GetRequiredService<HandlerCallLog>();

        await dispatcher.DispatchAsync(
            NewJob("greeting", """{"name":"Rithika"}"""),
            CancellationToken.None);

        Assert.Equal(["Rithika"], log.Calls);
    }

    [Fact]
    public async Task DispatchAsync_IsCaseInsensitive_ForJobType()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();
        var log = scope.ServiceProvider.GetRequiredService<HandlerCallLog>();

        await dispatcher.DispatchAsync(
            NewJob("GREETING", """{"name":"Rithika"}"""),
            CancellationToken.None);

        Assert.Single(log.Calls);
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenNoHandlerRegistered()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        var ex = await Assert.ThrowsAsync<HandlerNotFoundException>(
            () => dispatcher.DispatchAsync(
                NewJob("unknown-type", "{}"),
                CancellationToken.None));

        Assert.Equal("unknown-type", ex.JobType);
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenPayloadIsInvalid()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        await Assert.ThrowsAsync<InvalidPayloadException>(
            () => dispatcher.DispatchAsync(
                NewJob("greeting", "{ broken"),
                CancellationToken.None));
    }

    [Fact]
    public async Task DispatchAsync_PropagatesHandlerException_Unchanged()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcher>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(
                NewJob("failing-greeting", """{"name":"x"}"""),
                CancellationToken.None));

        Assert.Equal("handler failed", ex.Message);
    }

    [Fact]
    public void AddHandler_Throws_WhenJobTypeRegisteredTwice()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddJobHandlers(handlers => handlers
                .AddHandler<RecordingHandler, GreetingPayload>("greeting")
                .AddHandler<AnotherGreetingHandler, GreetingPayload>("greeting")));
        
        
    }
    
    [Fact]
    public void AddHandler_Throws_WhenPayloadTypeUsedTwice()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddJobHandlers(handlers => handlers
                .AddHandler<RecordingHandler, GreetingPayload>("greeting")
                .AddHandler<AnotherGreetingHandler, GreetingPayload>("greeting-2")));
    }
}