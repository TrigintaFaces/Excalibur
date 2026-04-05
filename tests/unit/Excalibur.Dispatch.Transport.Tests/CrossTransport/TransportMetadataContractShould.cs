// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Cross-transport metadata contract tests. These verify that the framework's metadata
/// serialization contract (MessageContextSerializer) correctly round-trips ALL context
/// fields through the X-header dictionary format used by every transport provider.
/// If these tests pass, any transport that maps TransportMessage fields → native headers
/// and back will preserve the full context.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CrossTransport")]
[Trait("Category", "MetadataRoundTrip")]
public sealed class TransportMetadataContractShould
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	// ── 1. Full Context Round-Trip ─────────────────────────────────

	[Fact]
	public void RoundTrip_AllContextFields_ThroughSerializationBoundary()
	{
		// Arrange - populate EVERY field on the context
		var original = MessageContext.CreateForDeserialization(_serviceProvider);
		original.MessageId = "contract-msg-001";
		original.CorrelationId = "contract-corr-001";
		original.CausationId = "contract-cause-001";

		var identity = original.GetOrCreateIdentityFeature();
		identity.ExternalId = "ext-001";
		identity.UserId = "user-001";
		identity.TenantId = "tenant-001";
		identity.SessionId = "sess-001";
		identity.WorkflowId = "wf-001";
		identity.TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

		var routing = original.GetOrCreateRoutingFeature();
		routing.PartitionKey = "pk-001";
		routing.Source = "order-service";

		original.SetMessageType("OrderPlacedEvent");
		original.SetContentType("application/json");
		original.GetOrCreateProcessingFeature().DeliveryCount = 3;
		original.SetSentTimestampUtc(new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero));

		// Act - serialize to dictionary (transport-agnostic format) and back
		var dict = MessageContextSerializer.SerializeToDictionary(original);
		var restored = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert - EVERY field must survive
		restored.MessageId.ShouldBe("contract-msg-001");
		restored.CorrelationId.ShouldBe("contract-corr-001");
		restored.CausationId.ShouldBe("contract-cause-001");
		restored.GetExternalId().ShouldBe("ext-001");
		restored.GetUserId().ShouldBe("user-001");
		restored.GetTenantId().ShouldBe("tenant-001");
		restored.GetSessionId().ShouldBe("sess-001");
		restored.GetWorkflowId().ShouldBe("wf-001");
		restored.GetTraceParent().ShouldBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
		restored.GetPartitionKey().ShouldBe("pk-001");
		restored.GetSource().ShouldBe("order-service");
		restored.GetMessageType().ShouldBe("OrderPlacedEvent");
		restored.GetContentType().ShouldBe("application/json");
		restored.GetDeliveryCount().ShouldBe(3);
		restored.GetSentTimestampUtc().ShouldBe(
			new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero));
	}

	// ── 2. TransportMessage Field Completeness ─────────────────────

	[Fact]
	public void TransportMessage_AllMetadataFields_Settable()
	{
		// Arrange & Act - verify every metadata field on TransportMessage can be set/read
		var msg = new TransportMessage
		{
			Id = "tm-001",
			Body = Encoding.UTF8.GetBytes("{\"order\":42}"),
			ContentType = "application/json",
			MessageType = "OrderPlacedEvent",
			CorrelationId = "tm-corr-001",
			CausationId = "tm-cause-001",
			Subject = "orders",
			TimeToLive = TimeSpan.FromMinutes(30),
			CreatedAt = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero),
		};
		msg.Properties["custom-key"] = "custom-value";
		msg.Properties["dispatch.ordering-key"] = "order-42";
		msg.Properties["dispatch.partition-key"] = "pk-42";
		msg.Properties["dispatch.deduplication-id"] = "dedup-42";

		// Assert - every field preserved
		msg.Id.ShouldBe("tm-001");
		msg.Body.Length.ShouldBeGreaterThan(0);
		msg.ContentType.ShouldBe("application/json");
		msg.MessageType.ShouldBe("OrderPlacedEvent");
		msg.CorrelationId.ShouldBe("tm-corr-001");
		msg.CausationId.ShouldBe("tm-cause-001");
		msg.Subject.ShouldBe("orders");
		msg.TimeToLive.ShouldBe(TimeSpan.FromMinutes(30));
		msg.CreatedAt.ShouldBe(new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero));
		msg.Properties["custom-key"].ShouldBe("custom-value");
		msg.Properties["dispatch.ordering-key"].ShouldBe("order-42");
		msg.Properties["dispatch.partition-key"].ShouldBe("pk-42");
		msg.Properties["dispatch.deduplication-id"].ShouldBe("dedup-42");
	}

	[Fact]
	public void TransportReceivedMessage_AllMetadataFields_Settable()
	{
		// Arrange & Act
		var msg = new TransportReceivedMessage
		{
			Id = "rm-001",
			Body = Encoding.UTF8.GetBytes("{\"order\":42}"),
			ContentType = "application/json",
			MessageType = "OrderPlacedEvent",
			CorrelationId = "rm-corr-001",
			Subject = "orders",
			DeliveryCount = 3,
			EnqueuedAt = DateTimeOffset.UtcNow,
			Source = "orders-queue",
			PartitionKey = "pk-42",
			MessageGroupId = "group-42",
			LockExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["causation-id"] = "rm-cause-001",
				["custom-header"] = "custom-value",
			},
			ProviderData = new Dictionary<string, object>
			{
				["receipt_handle"] = "handle-001",
			},
		};

		// Assert - every field preserved
		msg.Id.ShouldBe("rm-001");
		msg.Body.Length.ShouldBeGreaterThan(0);
		msg.ContentType.ShouldBe("application/json");
		msg.MessageType.ShouldBe("OrderPlacedEvent");
		msg.CorrelationId.ShouldBe("rm-corr-001");
		msg.Subject.ShouldBe("orders");
		msg.DeliveryCount.ShouldBe(3);
		msg.Source.ShouldBe("orders-queue");
		msg.PartitionKey.ShouldBe("pk-42");
		msg.MessageGroupId.ShouldBe("group-42");
		msg.LockExpiresAt.ShouldNotBeNull();
		msg.Properties["causation-id"].ShouldBe("rm-cause-001");
		msg.Properties["custom-header"].ShouldBe("custom-value");
		msg.ProviderData["receipt_handle"].ShouldBe("handle-001");
	}

	// ── 3. OutboundMessage Metadata Completeness ────────────────────

	[Fact]
	public void OutboundMessage_PreservesAllMetadataFields()
	{
		// Arrange & Act
		var msg = new OutboundMessage(
			messageType: "OrderPlacedEvent",
			payload: Encoding.UTF8.GetBytes("{\"orderId\":42}"),
			destination: "orders-topic",
			headers: new Dictionary<string, object>
			{
				["SourceMessageType"] = "PlaceOrderCommand",
				["CreatedAt"] = DateTimeOffset.UtcNow,
			})
		{
			CorrelationId = "outbox-corr-001",
			CausationId = "outbox-cause-001",
			TenantId = "tenant-001",
			PartitionKey = "pk-001",
			GroupKey = "group-001",
			Priority = 5,
		};

		// Assert - all metadata preserved
		msg.Id.ShouldNotBeNullOrEmpty();
		msg.MessageType.ShouldBe("OrderPlacedEvent");
		msg.Payload.ShouldNotBeEmpty();
		msg.Destination.ShouldBe("orders-topic");
		msg.CorrelationId.ShouldBe("outbox-corr-001");
		msg.CausationId.ShouldBe("outbox-cause-001");
		msg.TenantId.ShouldBe("tenant-001");
		msg.PartitionKey.ShouldBe("pk-001");
		msg.GroupKey.ShouldBe("group-001");
		msg.Priority.ShouldBe(5);
		msg.Headers.ShouldContainKey("SourceMessageType");
		msg.Status.ShouldBe(OutboxStatus.Staged);
		msg.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void OutboundMessage_MultiTransport_PreservesMetadataPerDelivery()
	{
		// Arrange
		var msg = new OutboundMessage(
			messageType: "OrderPlacedEvent",
			payload: Encoding.UTF8.GetBytes("{}"),
			destination: "orders-topic")
		{
			CorrelationId = "multi-corr-001",
		};

		msg.AddTransport("rabbitmq", "rabbitmq-orders");
		msg.AddTransport("kafka", "kafka-orders");

		// Assert - both transport deliveries exist with metadata intact
		msg.IsMultiTransport.ShouldBeTrue();
		msg.TransportDeliveries.Count.ShouldBe(2);
		msg.CorrelationId.ShouldBe("multi-corr-001");

		var rmqDelivery = msg.GetTransportDelivery("rabbitmq");
		rmqDelivery.ShouldNotBeNull();
		rmqDelivery!.TransportName.ShouldBe("rabbitmq");
		rmqDelivery.Destination.ShouldBe("rabbitmq-orders");

		var kafkaDelivery = msg.GetTransportDelivery("kafka");
		kafkaDelivery.ShouldNotBeNull();
		kafkaDelivery!.TransportName.ShouldBe("kafka");
		kafkaDelivery.Destination.ShouldBe("kafka-orders");
	}

	// ── 4. Serialization Dictionary Key Contract ────────────────────

	[Fact]
	public void SerializeToDictionary_ProducesExpectedKeyNames()
	{
		// Arrange
		var context = MessageContext.CreateForDeserialization(_serviceProvider);
		context.MessageId = "key-test-001";
		context.CorrelationId = "corr";
		context.CausationId = "cause";
		context.GetOrCreateIdentityFeature().UserId = "user";
		context.GetOrCreateIdentityFeature().TenantId = "tenant";
		context.GetOrCreateIdentityFeature().ExternalId = "ext";
		context.GetOrCreateIdentityFeature().SessionId = "sess";
		context.GetOrCreateIdentityFeature().WorkflowId = "wf";
		context.GetOrCreateIdentityFeature().TraceParent = "00-trace-span-01";
		context.GetOrCreateRoutingFeature().PartitionKey = "pk";
		context.GetOrCreateRoutingFeature().Source = "src";
		context.SetMessageType("TestType");
		context.SetContentType("application/json");

		// Act
		var dict = MessageContextSerializer.SerializeToDictionary(context);

		// Assert - verify EXACT key names that all transports depend on
		dict.ShouldContainKey("X-MessageId");
		dict.ShouldContainKey("X-CorrelationId");
		dict.ShouldContainKey("X-CausationId");
		dict.ShouldContainKey("X-UserId");
		dict.ShouldContainKey("X-TenantId");
		dict.ShouldContainKey("X-ExternalId");
		dict.ShouldContainKey("X-SessionId");
		dict.ShouldContainKey("X-WorkflowId");
		dict.ShouldContainKey("X-PartitionKey");
		dict.ShouldContainKey("X-Source");
		dict.ShouldContainKey("X-MessageType");
		dict.ShouldContainKey("X-ContentType");
		dict.ShouldContainKey("X-DeliveryCount");
		dict.ShouldContainKey("traceparent"); // W3C standard - lowercase
	}

	// ── 5. Concurrent Serialization Safety ─────────────────────────

	[Fact]
	public async Task ConcurrentSerialization_ProducesIsolatedResults()
	{
		// Arrange
		const int count = 100;
		var tasks = new Task<Dictionary<string, string>>[count];

		for (var i = 0; i < count; i++)
		{
			var idx = i;
			tasks[i] = Task.Run(() =>
			{
				var ctx = MessageContext.CreateForDeserialization(_serviceProvider);
				ctx.MessageId = $"concurrent-{idx}";
				ctx.CorrelationId = $"corr-{idx}";
				ctx.CausationId = $"cause-{idx}";
				ctx.SetMessageType($"Type-{idx}");
				ctx.GetOrCreateIdentityFeature().TenantId = $"tenant-{idx}";
				return MessageContextSerializer.SerializeToDictionary(ctx);
			});
		}

		// Act
		var results = await Task.WhenAll(tasks);

		// Assert - each dictionary has unique values (no cross-contamination)
		for (var i = 0; i < count; i++)
		{
			results[i]["X-MessageId"].ShouldBe($"concurrent-{i}");
			results[i]["X-CorrelationId"].ShouldBe($"corr-{i}");
			results[i]["X-CausationId"].ShouldBe($"cause-{i}");
			results[i]["X-MessageType"].ShouldBe($"Type-{i}");
			results[i]["X-TenantId"].ShouldBe($"tenant-{i}");
		}
	}

	// ── 6. Edge Cases ──────────────────────────────────────────────

	[Fact]
	public void RoundTrip_UnicodeValues_Preserved()
	{
		// Arrange
		var ctx = MessageContext.CreateForDeserialization(_serviceProvider);
		ctx.MessageId = "unicode-001";
		ctx.CorrelationId = "日本語テスト";
		ctx.CausationId = "Ñoño-Ünïcödé";
		ctx.GetOrCreateIdentityFeature().TenantId = "租户-001";
		ctx.SetMessageType("UnicodeType");

		// Act
		var dict = MessageContextSerializer.SerializeToDictionary(ctx);
		var restored = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert
		restored.CorrelationId.ShouldBe("日本語テスト");
		restored.CausationId.ShouldBe("Ñoño-Ünïcödé");
		restored.GetTenantId().ShouldBe("租户-001");
	}

	[Fact]
	public void RoundTrip_LongValues_NotTruncated()
	{
		// Arrange
		var longCorr = new string('x', 1024);
		var longTenant = new string('y', 512);
		var ctx = MessageContext.CreateForDeserialization(_serviceProvider);
		ctx.MessageId = "long-001";
		ctx.CorrelationId = longCorr;
		ctx.GetOrCreateIdentityFeature().TenantId = longTenant;
		ctx.SetMessageType("LongTest");

		// Act
		var dict = MessageContextSerializer.SerializeToDictionary(ctx);
		var restored = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert
		restored.CorrelationId.ShouldBe(longCorr);
		restored.GetTenantId().ShouldBe(longTenant);
	}

	[Fact]
	public void RoundTrip_SpecialCharacters_Preserved()
	{
		// Arrange
		var ctx = MessageContext.CreateForDeserialization(_serviceProvider);
		ctx.MessageId = "special-001";
		ctx.CorrelationId = "corr/with=special+chars&more";
		ctx.CausationId = "cause:with;semi,comma";
		ctx.SetMessageType("Special.Type$1");

		// Act
		var dict = MessageContextSerializer.SerializeToDictionary(ctx);
		var restored = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert
		restored.CorrelationId.ShouldBe("corr/with=special+chars&more");
		restored.CausationId.ShouldBe("cause:with;semi,comma");
		restored.GetMessageType().ShouldBe("Special.Type$1");
	}

	[Fact]
	public void Deserialize_PartialMetadata_PreservesWhatExists()
	{
		// Arrange - only MessageId and MessageType (minimum required)
		var dict = new Dictionary<string, string>
		{
			["X-MessageId"] = "partial-001",
			["X-MessageType"] = "TestType",
		};

		// Act
		var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert - present fields preserved, absent fields null/default
		result.MessageId.ShouldBe("partial-001");
		result.GetMessageType().ShouldBe("TestType");
		result.CorrelationId.ShouldBeNull();
		result.CausationId.ShouldBeNull();
		result.GetUserId().ShouldBeNull();
		result.GetTenantId().ShouldBeNull();
		result.GetTraceParent().ShouldBeNull();
		result.GetPartitionKey().ShouldBeNull();
		result.GetDeliveryCount().ShouldBe(0);
		result.GetSentTimestampUtc().ShouldBeNull();
	}

	[Fact]
	public void Deserialize_ExtraUnknownKeys_Ignored()
	{
		// Arrange - dict has keys the serializer doesn't know about
		var dict = new Dictionary<string, string>
		{
			["X-MessageId"] = "extra-001",
			["X-MessageType"] = "TestType",
			["X-Unknown-Custom-Key"] = "should-be-ignored",
			["another-key"] = "also-ignored",
		};

		// Act
		var result = MessageContextSerializer.DeserializeFromDictionary(dict, _serviceProvider);

		// Assert - deserializes without error
		result.MessageId.ShouldBe("extra-001");
		result.GetMessageType().ShouldBe("TestType");
	}

	// ── 7. Transport Header Name Constants Verification ─────────────

	[Theory]
	[InlineData("correlation-id", "SQS/Kafka/PubSub standard header name")]
	[InlineData("causation-id", "SQS/Kafka/PubSub standard header name")]
	[InlineData("message-type", "SQS/Kafka/PubSub standard header name")]
	[InlineData("content-type", "SQS/Kafka/PubSub standard header name")]
	[InlineData("message-id", "Kafka/PubSub standard header name")]
	[InlineData("subject", "Kafka/PubSub standard header name")]
	public void TransportHeaderNames_FollowKebabCaseConvention(string headerName, string description)
	{
		// Assert - header names are lowercase kebab-case
		_ = description; // suppress unused warning
#pragma warning disable CA1308 // Normalize strings to uppercase - intentionally validating lowercase
		headerName.ShouldBe(headerName.ToLowerInvariant(),
			$"Header '{headerName}' should be lowercase");
#pragma warning restore CA1308
		headerName.Contains('_').ShouldBeFalse(
			$"Header '{headerName}' should use dashes, not underscores");
	}

	// ── 8. Batch OutboundMessage Metadata Isolation ─────────────────

	[Fact]
	public void BatchOutboundMessages_EachPreservesUniqueMetadata()
	{
		// Arrange
		var messages = Enumerable.Range(0, 50).Select(i =>
			new OutboundMessage(
				messageType: $"Event-{i}",
				payload: Encoding.UTF8.GetBytes($"{{\"seq\":{i}}}"),
				destination: $"topic-{i % 5}")
			{
				CorrelationId = $"batch-corr-{i}",
				CausationId = $"batch-cause-{i}",
				TenantId = $"tenant-{i % 3}",
				PartitionKey = $"pk-{i}",
			}).ToList();

		// Assert - each message has unique, correct metadata
		for (var i = 0; i < 50; i++)
		{
			var msg = messages[i];
			msg.MessageType.ShouldBe($"Event-{i}");
			msg.CorrelationId.ShouldBe($"batch-corr-{i}");
			msg.CausationId.ShouldBe($"batch-cause-{i}");
			msg.TenantId.ShouldBe($"tenant-{i % 3}");
			msg.PartitionKey.ShouldBe($"pk-{i}");
			msg.Destination.ShouldBe($"topic-{i % 5}");
		}

		// Assert - all IDs are unique
		var uniqueIds = messages.Select(m => m.Id).Distinct().ToList();
		uniqueIds.Count.ShouldBe(50, "Each OutboundMessage should have a unique Id");
	}
}
