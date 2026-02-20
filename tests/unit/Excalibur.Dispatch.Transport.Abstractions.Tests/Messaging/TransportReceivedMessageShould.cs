using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class TransportReceivedMessageShould
{
    [Fact]
    public void Should_Default_Id_To_Empty_String()
    {
        var message = new TransportReceivedMessage();

        message.Id.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_Body_To_Empty()
    {
        var message = new TransportReceivedMessage();

        message.Body.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_DeliveryCount_To_Zero()
    {
        var message = new TransportReceivedMessage();

        message.DeliveryCount.ShouldBe(0);
    }

    [Fact]
    public void Should_Default_Properties_To_Empty_Dictionary()
    {
        var message = new TransportReceivedMessage();

        message.Properties.ShouldNotBeNull();
        message.Properties.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Default_ProviderData_To_Empty_Dictionary()
    {
        var message = new TransportReceivedMessage();

        message.ProviderData.ShouldNotBeNull();
        message.ProviderData.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var now = DateTimeOffset.UtcNow;
        var lockExpiry = now.AddMinutes(5);

        var message = new TransportReceivedMessage
        {
            Id = "msg-1",
            Body = new byte[] { 1, 2 },
            ContentType = "application/json",
            MessageType = "TestEvent",
            CorrelationId = "corr-1",
            Subject = "test",
            DeliveryCount = 3,
            EnqueuedAt = now,
            Source = "my-queue",
            PartitionKey = "pk-1",
            MessageGroupId = "group-1",
            LockExpiresAt = lockExpiry,
        };

        message.Id.ShouldBe("msg-1");
        message.ContentType.ShouldBe("application/json");
        message.MessageType.ShouldBe("TestEvent");
        message.CorrelationId.ShouldBe("corr-1");
        message.Subject.ShouldBe("test");
        message.DeliveryCount.ShouldBe(3);
        message.EnqueuedAt.ShouldBe(now);
        message.Source.ShouldBe("my-queue");
        message.PartitionKey.ShouldBe("pk-1");
        message.MessageGroupId.ShouldBe("group-1");
        message.LockExpiresAt.ShouldBe(lockExpiry);
    }

    [Fact]
    public void Should_Use_Ordinal_Comparison_For_Properties()
    {
        var props = new Dictionary<string, object>(StringComparer.Ordinal) { ["Key"] = "value" };
        var message = new TransportReceivedMessage { Properties = props };

        message.Properties.TryGetValue("Key", out var val).ShouldBeTrue();
        val.ShouldBe("value");

        message.Properties.TryGetValue("key", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Allow_Init_ProviderData()
    {
        var data = new Dictionary<string, object> { ["ReceiptHandle"] = "handle-123" };
        var message = new TransportReceivedMessage { ProviderData = data };

        message.ProviderData["ReceiptHandle"].ShouldBe("handle-123");
    }

    [Fact]
    public void Should_Default_Nullable_Properties_To_Null()
    {
        var message = new TransportReceivedMessage();

        message.ContentType.ShouldBeNull();
        message.MessageType.ShouldBeNull();
        message.CorrelationId.ShouldBeNull();
        message.Subject.ShouldBeNull();
        message.Source.ShouldBeNull();
        message.PartitionKey.ShouldBeNull();
        message.MessageGroupId.ShouldBeNull();
        message.LockExpiresAt.ShouldBeNull();
    }
}
