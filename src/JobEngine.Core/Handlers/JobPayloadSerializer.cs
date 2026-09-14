using System.Text.Json;

namespace JobEngine.Core.Handlers;

public static class JobPayloadSerializer
{
    public static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web);

    public static TPayload Deserialize<TPayload>(string payloadJson)
    {
        TPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<TPayload>(payloadJson, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidPayloadException(
                $"Payload could not be deserialized to {typeof(TPayload).Name}.", ex);
        }

        if (payload is null)
        {
            throw new InvalidPayloadException(
                $"Payload deserialized to null for {typeof(TPayload).Name}.");
        }

        return payload;
    }
}