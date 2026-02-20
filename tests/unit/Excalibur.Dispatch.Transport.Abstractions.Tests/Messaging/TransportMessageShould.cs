using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class TransportMessageShould
{
    [Fact]
    public void Should_Generate_Unique_Id_By_Default()
    {
        var message1 = new TransportMessage();
        var message2 = new TransportMessage();

        message1.Id.ShouldNotBeNullOrEmpty();
        message2.Id.ShouldNotBeNullOrEmpty();
        message1.Id.ShouldNotBe(message2.Id);
    }

    [Fact]
    public void Should_Allow_Setting_Id()
    {
        var message = new TransportMessage { Id = "custom-id" };

        message.Id.ShouldBe("custom-id");
    }

    [Fact]
    public void Should_Default_Body_To_Empty()
    {
        var message = new TransportMessage();

        message.Body.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Should_Store_Body_Content()
    {
        var body = Encoding.UTF8.GetBytes("hello");
        var message = new TransportMessage { Body = body };

        message.Body.ToArray().ShouldBe(body);
    }

    [Fact]
    public void Should_Default_ContentType_To_Null()
    {
        var message = new TransportMessage();

        message.ContentType.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_MessageType_To_Null()
    {
        var message = new TransportMessage();

        message.MessageType.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_CorrelationId_To_Null()
    {
        var message = new TransportMessage();

        message.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_Subject_To_Null()
    {
        var message = new TransportMessage();

        message.Subject.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_TimeToLive_To_Null()
    {
        var message = new TransportMessage();

        message.TimeToLive.ShouldBeNull();
    }

    [Fact]
    public void Should_Set_CreatedAt_To_UtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var message = new TransportMessage();
        var after = DateTimeOffset.UtcNow;

        message.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        message.CreatedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Should_Lazily_Initialize_Properties()
    {
        var message = new TransportMessage();

        // Before accessing Properties, HasProperties should be false
        message.HasProperties.ShouldBeFalse();

        // Accessing Properties should create the dictionary
        message.Properties["key"] = "value";
        message.HasProperties.ShouldBeTrue();
    }

    [Fact]
    public void Should_Return_False_For_HasProperties_When_Empty_Dictionary_Accessed()
    {
        var message = new TransportMessage();

        // Access Properties to force initialization but don't add anything
        _ = message.Properties;

        // HasProperties checks Count > 0, not just allocation
        message.HasProperties.ShouldBeFalse();
    }

    [Fact]
    public void Should_Allow_Init_Properties()
    {
        var props = new Dictionary<string, object> { ["key"] = "value" };
        var message = new TransportMessage { Properties = props };

        message.Properties.ShouldBeSameAs(props);
        message.HasProperties.ShouldBeTrue();
    }

    [Fact]
    public void FromBytes_Should_Create_Message_With_Body()
    {
        var body = new byte[] { 1, 2, 3 };

        var message = TransportMessage.FromBytes(body);

        message.Body.ToArray().ShouldBe(body);
    }

    [Fact]
    public void FromString_Should_Create_Message_With_Utf8_Body()
    {
        var text = "Hello, World!";

        var message = TransportMessage.FromString(text);

        Encoding.UTF8.GetString(message.Body.Span).ShouldBe(text);
    }

    [Fact]
    public void FromString_Should_Set_ContentType_To_TextPlain()
    {
        var message = TransportMessage.FromString("test");

        message.ContentType.ShouldBe("text/plain");
    }

    [Fact]
    public void Should_Set_All_Properties()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new TransportMessage
        {
            Id = "id-1",
            Body = new byte[] { 0xFF },
            ContentType = "application/json",
            MessageType = "OrderCreated",
            CorrelationId = "corr-1",
            Subject = "orders",
            TimeToLive = TimeSpan.FromMinutes(5),
            CreatedAt = now,
        };

        message.Id.ShouldBe("id-1");
        message.ContentType.ShouldBe("application/json");
        message.MessageType.ShouldBe("OrderCreated");
        message.CorrelationId.ShouldBe("corr-1");
        message.Subject.ShouldBe("orders");
        message.TimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
        message.CreatedAt.ShouldBe(now);
    }
}
