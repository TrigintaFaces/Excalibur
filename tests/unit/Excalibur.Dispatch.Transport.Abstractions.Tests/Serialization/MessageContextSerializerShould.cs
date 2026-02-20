using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageContextSerializerShould
{
    private static readonly IServiceProvider NullServiceProvider = new NullProvider();

    [Fact]
    public void SerializeToDictionary_Throws_WhenContextIsNull()
    {
        Should.Throw<ArgumentNullException>(() => MessageContextSerializer.SerializeToDictionary(null!));
    }

    [Fact]
    public void SerializeToDictionary_WritesExpectedFields_AndSkipsEmptyOnes()
    {
        var context = new MessageEnvelope
        {
            MessageId = "msg-123",
            ExternalId = null,
            UserId = "user-1",
            CorrelationId = "corr-1",
            CausationId = "cause-1",
            TenantId = "tenant-1",
            SessionId = "session-1",
            WorkflowId = "workflow-1",
            PartitionKey = "pk-1",
            Source = "orders",
            MessageType = "OrderCreated",
            ContentType = "application/json",
            TraceParent = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            DeliveryCount = 4,
            SentTimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(123456789)
        };

        var attributes = MessageContextSerializer.SerializeToDictionary(context);

        attributes["X-MessageId"].ShouldBe("msg-123");
        attributes.ContainsKey("X-ExternalId").ShouldBeFalse();
        attributes["X-UserId"].ShouldBe("user-1");
        attributes["X-CorrelationId"].ShouldBe("corr-1");
        attributes["X-CausationId"].ShouldBe("cause-1");
        attributes["X-TenantId"].ShouldBe("tenant-1");
        attributes["X-SessionId"].ShouldBe("session-1");
        attributes["X-WorkflowId"].ShouldBe("workflow-1");
        attributes["X-PartitionKey"].ShouldBe("pk-1");
        attributes["X-Source"].ShouldBe("orders");
        attributes["X-MessageType"].ShouldBe("OrderCreated");
        attributes["X-ContentType"].ShouldBe("application/json");
        attributes["traceparent"].ShouldBe("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        attributes["X-DeliveryCount"].ShouldBe("4");
        attributes["X-SentTimestamp"].ShouldBe("123456789");
    }

    [Fact]
    public void DeserializeFromDictionary_Throws_WhenArgumentsAreNull()
    {
        Should.Throw<ArgumentNullException>(() => MessageContextSerializer.DeserializeFromDictionary(null!, NullServiceProvider));
        Should.Throw<ArgumentNullException>(() => MessageContextSerializer.DeserializeFromDictionary(new Dictionary<string, string>(), null!));
    }

    [Fact]
    public void DeserializeFromDictionary_Throws_WhenMessageIdIsMissing()
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-MessageType"] = "OrderCreated"
        };

        var ex = Should.Throw<InvalidOperationException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(attributes, NullServiceProvider));

        ex.Message.ShouldContain("MessageId");
    }

    [Fact]
    public void DeserializeFromDictionary_Throws_WhenMessageTypeIsMissing()
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-MessageId"] = "msg-123"
        };

        var ex = Should.Throw<InvalidOperationException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(attributes, NullServiceProvider));

        ex.Message.ShouldContain("MessageType");
    }

    [Fact]
    public void DeserializeFromDictionary_ReadsExpectedFields_AndParsesNumericValues()
    {
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-3).ToUnixTimeMilliseconds();
        var before = DateTimeOffset.UtcNow;

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-MessageId"] = "msg-123",
            ["X-UserId"] = "user-1",
            ["X-CorrelationId"] = "corr-1",
            ["X-CausationId"] = "cause-1",
            ["X-TenantId"] = "tenant-1",
            ["X-SessionId"] = "session-1",
            ["X-WorkflowId"] = "workflow-1",
            ["X-PartitionKey"] = "partition-1",
            ["X-Source"] = "orders",
            ["X-MessageType"] = "OrderCreated",
            ["X-ContentType"] = "application/json",
            ["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            ["X-DeliveryCount"] = "7",
            ["X-SentTimestamp"] = sentAt.ToString()
        };

        var context = MessageContextSerializer.DeserializeFromDictionary(attributes, NullServiceProvider);

        context.MessageId.ShouldBe("msg-123");
        context.UserId.ShouldBe("user-1");
        context.CorrelationId.ShouldBe("corr-1");
        context.CausationId.ShouldBe("cause-1");
        context.TenantId.ShouldBe("tenant-1");
        context.SessionId.ShouldBe("session-1");
        context.WorkflowId.ShouldBe("workflow-1");
        context.PartitionKey.ShouldBe("partition-1");
        context.Source.ShouldBe("orders");
        context.MessageType.ShouldBe("OrderCreated");
        context.ContentType.ShouldBe("application/json");
        context.TraceParent.ShouldBe("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        context.DeliveryCount.ShouldBe(7);
        context.SentTimestampUtc.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(sentAt));
        context.ReceivedTimestampUtc.ShouldBeGreaterThan(before.AddSeconds(-1));
    }

    [Fact]
    public void DeserializeFromDictionary_LeavesDefaults_WhenNumericFieldsAreInvalid()
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-MessageId"] = "msg-123",
            ["X-MessageType"] = "OrderCreated",
            ["X-DeliveryCount"] = "not-a-number",
            ["X-SentTimestamp"] = "bad-timestamp"
        };

        var context = MessageContextSerializer.DeserializeFromDictionary(attributes, NullServiceProvider);

        context.DeliveryCount.ShouldBe(0);
        context.SentTimestampUtc.ShouldBeNull();
    }

    private sealed class NullProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
