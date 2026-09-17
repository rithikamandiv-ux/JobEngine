namespace JobEngine.SampleHandlers;

public class FlakyPayload
{
    public string Message { get; set; } = "";

    public int FailUntilAttempt { get; set; }
}