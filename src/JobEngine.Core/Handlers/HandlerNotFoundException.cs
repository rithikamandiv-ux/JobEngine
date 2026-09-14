namespace JobEngine.Core.Handlers;

public class HandlerNotFoundException : Exception
{
    public HandlerNotFoundException(string jobType)
        : base($"No handler is registered for job type '{jobType}'.")
    {
        JobType = jobType;
    }

    public string JobType { get; }
}