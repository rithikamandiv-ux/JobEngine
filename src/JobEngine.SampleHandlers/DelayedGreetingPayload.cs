namespace JobEngine.SampleHandlers;

public class DelayedGreetingPayload
{
    public string Message { get; set; } = "";

    public int DelayMilliseconds { get; set; }
}