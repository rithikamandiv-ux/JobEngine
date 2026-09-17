namespace JobEngine.Core.Retry;

public class RetryOptions
{
    public const string SectionName = "Retry";

    public int BaseDelaySeconds { get; set; } = 2;

    public int MaxDelaySeconds { get; set; } = 300;

    public bool UseJitter { get; set; } = true;
}