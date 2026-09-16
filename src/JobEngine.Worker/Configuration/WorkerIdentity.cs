namespace JobEngine.Worker.Configuration;

public class WorkerIdentity
{
    public WorkerIdentity(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public static WorkerIdentity Create(string? configuredName)
    {
        var name = string.IsNullOrWhiteSpace(configuredName)
            ? Environment.MachineName
            : configuredName;

        var suffix = Guid.NewGuid().ToString("N")[..8];

        return new WorkerIdentity($"{name}-{suffix}");
    }
}