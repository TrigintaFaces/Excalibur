// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageContextSerializerShould
{
    private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

    [Fact]
    public void Throw_When_Context_Is_Null_On_Serialize()
    {
        Should.Throw<ArgumentNullException>(() =>
            MessageContextSerializer.SerializeToDictionary(null!));
    }

    [Fact]
    public void Throw_When_Attributes_Are_Null_On_Deserialize()
    {
        Should.Throw<ArgumentNullException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(null!, _serviceProvider));
    }

    [Fact]
    public void Throw_When_ServiceProvider_Is_Null_On_Deserialize()
    {
        var dict = new Dictionary<string, string>();
        Should.Throw<ArgumentNullException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(dict, null!));
    }

    [Fact]
    public void Serialize_String_Fields()
    {
        var context = MessageContext.CreateForDeserialization(_serviceProvider);
        context.MessageId = "msg-1";
        context.ExternalId = "ext-1";
        context.UserId = "user-1";
        context.CorrelationId = "corr-1";
        context.CausationId = "cause-1";
        context.TenantId = "tenant-1";
        context.SessionId = "sess-1";
        context.WorkflowId = "wf-1";
        context.PartitionKey = "pk-1";
        context.Source = "src-1";
        context.MessageType = "OrderCreated";
        context.ContentType = "application/json";
        context.TraceParent = "00-abc-def-01";

        var dict = MessageContextSerializer.SerializeToDictionary(context);

        dict["X-MessageId"].ShouldBe("msg-1");
        dict["X-ExternalId"].ShouldBe("ext-1");
        dict["X-UserId"].ShouldBe("user-1");
        dict["X-CorrelationId"].ShouldBe("corr-1");
        dict["X-CausationId"].ShouldBe("cause-1");
        dict["X-TenantId"].ShouldBe("tenant-1");
        dict["X-SessionId"].ShouldBe("sess-1");
        dict["X-WorkflowId"].ShouldBe("wf-1");
        dict["X-PartitionKey"].ShouldBe("pk-1");
        dict["X-Source"].ShouldBe("src-1");
        dict["X-MessageType"].ShouldBe("OrderCreated");
        dict["X-ContentType"].ShouldBe("application/json");
        dict["traceparent"].ShouldBe("00-abc-def-01");
    }

    [Fact]
    public void Serialize_DeliveryCount()
    {
        var context = MessageContext.CreateForDeserialization(_serviceProvider);
        context.MessageId = "msg-1";
        context.DeliveryCount = 5;

        var dict = MessageContextSerializer.SerializeToDictionary(context);

        dict["X-DeliveryCount"].ShouldBe("5");
    }

    [Fact]
    public void Serialize_SentTimestampUtc()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var context = MessageContext.CreateForDeserialization(_serviceProvider);
        context.MessageId = "msg-1";
        context.SentTimestampUtc = timestamp;

        var dict = MessageContextSerializer.SerializeToDictionary(context);

        dict["X-SentTimestamp"].ShouldBe(timestamp.ToUnixTimeMilliseconds().ToString());
    }

    [Fact]
    public void Not_Serialize_Null_Or_Empty_Fields()
    {
        var context = MessageContext.CreateForDeserialization(_serviceProvider);
        context.MessageId = "msg-1";
        // Leave other string fields null/empty

        var dict = MessageContextSerializer.SerializeToDictionary(context);

        dict.ShouldContainKey("X-MessageId");
        dict.ShouldNotContainKey("X-ExternalId");
        dict.ShouldNotContainKey("X-UserId");
        dict.ShouldNotContainKey("X-TenantId");
    }

    [Fact]
    public void Not_Serialize_Null_SentTimestamp()
    {
        var context = MessageContext.CreateForDeserialization(_serviceProvider);
        context.MessageId = "msg-1";
        context.SentTimestampUtc = null;

        var dict = MessageContextSerializer.SerializeToDictionary(context);
        dict.ShouldNotContainKey("X-SentTimestamp");
    }

    [Fact]
    public void Deserialize_All_String_Fields()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-ExternalId"] = "ext-1",
            ["X-UserId"] = "user-1",
            ["X-CorrelationId"] = "corr-1",
            ["X-CausationId"] = "cause-1",
            ["X-TenantId"] = "tenant-1",
            ["X-SessionId"] = "sess-1",
            ["X-WorkflowId"] = "wf-1",
            ["X-PartitionKey"] = "pk-1",
            ["X-Source"] = "src-1",
            ["X-MessageType"] = "OrderCreated",
            ["X-ContentType"] = "application/json",
            ["traceparent"] = "00-abc-def-01",
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

        result.MessageId.ShouldBe("msg-1");
        result.ExternalId.ShouldBe("ext-1");
        result.UserId.ShouldBe("user-1");
        result.CorrelationId.ShouldBe("corr-1");
        result.CausationId.ShouldBe("cause-1");
        result.TenantId.ShouldBe("tenant-1");
        result.SessionId.ShouldBe("sess-1");
        result.WorkflowId.ShouldBe("wf-1");
        result.PartitionKey.ShouldBe("pk-1");
        result.Source.ShouldBe("src-1");
        result.MessageType.ShouldBe("OrderCreated");
        result.ContentType.ShouldBe("application/json");
        result.TraceParent.ShouldBe("00-abc-def-01");
    }

    [Fact]
    public void Deserialize_DeliveryCount()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-MessageType"] = "Test",
            ["X-DeliveryCount"] = "7",
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);
        result.DeliveryCount.ShouldBe(7);
    }

    [Fact]
    public void Deserialize_SentTimestamp()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-MessageType"] = "Test",
            ["X-SentTimestamp"] = timestamp.ToUnixTimeMilliseconds().ToString(),
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);
        result.SentTimestampUtc.ShouldNotBeNull();
        result.SentTimestampUtc!.Value.ShouldBe(timestamp);
    }

    [Fact]
    public void Set_ReceivedTimestampUtc_On_Deserialize()
    {
        var before = DateTimeOffset.UtcNow;
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-MessageType"] = "Test",
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);
        var after = DateTimeOffset.UtcNow;

        result.ReceivedTimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
        result.ReceivedTimestampUtc.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Throw_When_MessageId_Missing_On_Deserialize()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageType"] = "Test",
        };

        Should.Throw<InvalidOperationException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider))
            .Message.ShouldContain("MessageId");
    }

    [Fact]
    public void Throw_When_MessageType_Missing_On_Deserialize()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
        };

        Should.Throw<InvalidOperationException>(() =>
            MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider))
            .Message.ShouldContain("MessageType");
    }

    [Fact]
    public void Roundtrip_Serialize_And_Deserialize()
    {
        var original = MessageContext.CreateForDeserialization(_serviceProvider);
        original.MessageId = "roundtrip-1";
        original.CorrelationId = "corr-rt";
        original.MessageType = "RoundTripTest";
        original.DeliveryCount = 3;
        original.SentTimestampUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var dict = MessageContextSerializer.SerializeToDictionary(original);
        var restored = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

        restored.MessageId.ShouldBe("roundtrip-1");
        restored.CorrelationId.ShouldBe("corr-rt");
        restored.MessageType.ShouldBe("RoundTripTest");
        restored.DeliveryCount.ShouldBe(3);
        restored.SentTimestampUtc.ShouldBe(original.SentTimestampUtc);
    }

    [Fact]
    public void Handle_Invalid_DeliveryCount_Gracefully()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-MessageType"] = "Test",
            ["X-DeliveryCount"] = "not-a-number",
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);
        result.DeliveryCount.ShouldBe(0);
    }

    [Fact]
    public void Handle_Invalid_SentTimestamp_Gracefully()
    {
        var dict = new Dictionary<string, string>
        {
            ["X-MessageId"] = "msg-1",
            ["X-MessageType"] = "Test",
            ["X-SentTimestamp"] = "invalid",
        };

        var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);
        result.SentTimestampUtc.ShouldBeNull();
    }
}
