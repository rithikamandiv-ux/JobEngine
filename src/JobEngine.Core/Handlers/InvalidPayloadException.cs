namespace JobEngine.Core.Handlers;

public class InvalidPayloadException : Exception
{
    public InvalidPayloadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}