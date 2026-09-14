using JobEngine.Core.Handlers;

namespace JobEngine.Tests.Handlers;

public class JobPayloadSerializerTests
{
    [Fact]
    public void Deserialize_ReturnsPayload_WhenJsonIsValid()
    {
        var payload = JobPayloadSerializer
            .Deserialize<GreetingPayload>("""{"name":"Rithika"}""");

        Assert.Equal("Rithika", payload.Name);
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive()
    {
        var payload = JobPayloadSerializer
            .Deserialize<GreetingPayload>("""{"Name":"Rithika"}""");

        Assert.Equal("Rithika", payload.Name);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ broken")]
    [InlineData("null")]
    public void Deserialize_Throws_WhenPayloadIsInvalid(string json)
    {
        Assert.Throws<InvalidPayloadException>(
            () => JobPayloadSerializer.Deserialize<GreetingPayload>(json));
    }

    [Fact]
    public void Deserialize_PreservesInnerException_WhenJsonIsMalformed()
    {
        var ex = Assert.Throws<InvalidPayloadException>(
            () => JobPayloadSerializer.Deserialize<GreetingPayload>("{ broken"));

        Assert.NotNull(ex.InnerException);
    }
}