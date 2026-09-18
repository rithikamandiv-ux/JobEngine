using System.Collections.Concurrent;
using JobEngine.Core;
using JobEngine.Core.Handlers;
using JobEngine.Core.Storage;
using JobEngine.Core.Retry;

namespace JobEngine.Tests.Infrastructure;

public class ExecutionRecorder
{
    private readonly ConcurrentDictionary<long, int> _executions = new();

    public void Record(long jobId) =>
        _executions.AddOrUpdate(jobId, 1, (_, count) => count + 1);

    public int CountFor(long jobId) => _executions.GetValueOrDefault(jobId);

    public IReadOnlyDictionary<long, int> All => _executions;

    public int TotalExecutions => _executions.Values.Sum();

    public IEnumerable<long> JobsExecutedMoreThanOnce =>
        _executions.Where(pair => pair.Value > 1).Select(pair => pair.Key);
}

public class CountingPayload
{
    public int WorkMilliseconds { get; set; }
}

public class CountingHandler : IJobHandler<CountingPayload>
{
    private readonly ExecutionRecorder _recorder;

    public CountingHandler(ExecutionRecorder recorder)
    {
        _recorder = recorder;
    }

    public async Task HandleAsync(
        CountingPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        _recorder.Record(context.JobId);

        if (payload.WorkMilliseconds > 0)
        {
            await Task.Delay(payload.WorkMilliseconds, cancellationToken);
        }
    }
}