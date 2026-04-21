// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Serialization round-trip contract tests for domain events.
/// Validates that JSON property names, types, and values survive serialize→deserialize cycles
/// and that property renames are detected as breaking changes.
/// </summary>
/// <remarks>
/// Sprint 693, Task T.1 (bd-jcswc): Closes the critical gap where JSON property renames
/// or type changes could silently break deserialization of persisted events.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class DomainEventSerializationContractShould
{
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	private static JsonEventSerializer CreateSerializer(JsonSerializerOptions? options = null)
	{
		return new JsonEventSerializer(options);
	}

	#region Round-Trip: All IDomainEvent Properties Preserved

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveAllProperties_WhenRoundTrippingSimpleEvent()
	{
		// Arrange
		var serializer = CreateSerializer();
		var occurredAt = new DateTimeOffset(2026, 3, 22, 10, 30, 0, TimeSpan.Zero);
		var original = new SimpleContractEvent
		{
			EventId = "evt-001",
			AggregateId = "agg-123",
			Version = 42,
			OccurredAt = occurredAt,
			OrderId = "order-456",
			Amount = 99.95m,
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert - ALL IDomainEvent properties
		deserialized.EventId.ShouldBe(original.EventId);
		deserialized.AggregateId.ShouldBe(original.AggregateId);
		deserialized.Version.ShouldBe(original.Version);
		deserialized.OccurredAt.ShouldBe(original.OccurredAt);
		deserialized.EventType.ShouldBe(original.EventType);

		// Assert - domain-specific properties
		deserialized.OrderId.ShouldBe("order-456");
		deserialized.Amount.ShouldBe(99.95m);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveMetadata_WhenRoundTripping()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = new SimpleContractEvent
		{
			EventId = "evt-meta",
			AggregateId = "agg-meta",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			OrderId = "order-1",
			Amount = 10m,
			Metadata = new Dictionary<string, object>
			{
				["CorrelationId"] = "corr-123",
				["UserId"] = "user-456",
				["TenantId"] = "tenant-789",
			},
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert
		deserialized.Metadata.ShouldNotBeNull();
		deserialized.Metadata.Count.ShouldBe(3);
		deserialized.Metadata["CorrelationId"].ToString().ShouldBe("corr-123");
		deserialized.Metadata["UserId"].ToString().ShouldBe("user-456");
		deserialized.Metadata["TenantId"].ToString().ShouldBe("tenant-789");
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveNullMetadata_WhenRoundTripping()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = new SimpleContractEvent
		{
			EventId = "evt-null-meta",
			AggregateId = "agg-null",
			Version = 0,
			OccurredAt = DateTimeOffset.UtcNow,
			OrderId = "order-1",
			Amount = 0m,
			Metadata = null,
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert - null metadata stays null (WhenWritingNull default)
		deserialized.Metadata.ShouldBeNull();
	}

	#endregion

	#region Contract Detection: JSON Property Name Changes Break Deserialization

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void DetectPropertyRename_WhenJsonPropertyNameChanges()
	{
		// Arrange - serialize with the "original" property name
		var serializer = CreateSerializer();
		var originalJson = """
			{
				"eventId": "evt-rename",
				"aggregateId": "agg-rename",
				"version": 1,
				"occurredAt": "2026-03-22T10:00:00+00:00",
				"eventType": "EventWithJsonPropertyName",
				"customerId": "cust-original"
			}
			""";
		var bytes = Encoding.UTF8.GetBytes(originalJson);

		// Act - deserialize into type where property name matches
		var deserialized = (EventWithJsonPropertyName)serializer.DeserializeEvent(
			bytes, typeof(EventWithJsonPropertyName));

		// Assert - the customerId property should survive because the JSON key matches
		deserialized.CustomerId.ShouldBe("cust-original");
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void LoseData_WhenJsonPropertyNameIsRenamed()
	{
		// Arrange - JSON was persisted with old property name "customer_id"
		var persistedJson = """
			{
				"eventId": "evt-renamed",
				"aggregateId": "agg-renamed",
				"version": 1,
				"occurredAt": "2026-03-22T10:00:00+00:00",
				"eventType": "EventWithRenamedProperty",
				"customer_id": "cust-old-name"
			}
			""";
		var bytes = Encoding.UTF8.GetBytes(persistedJson);

		// Act - deserialize into type where the JsonPropertyName was changed
		// The type now expects "client_id" instead of "customer_id"
		var deserialized = (EventWithRenamedProperty)serializer.DeserializeEvent(
			bytes, typeof(EventWithRenamedProperty));

		// Assert - the data is LOST because the JSON key no longer matches
		// This is the exact scenario this test suite protects against
		deserialized.ClientId.ShouldBeNull();
	}

	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	private readonly JsonEventSerializer serializer = CreateSerializer();

	#endregion

	#region Fixed JSON Contract Snapshots

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ProduceStableJsonKeys_ForDomainEvent()
	{
		// Arrange
		var evt = new SimpleContractEvent
		{
			EventId = "evt-stable",
			AggregateId = "agg-stable",
			Version = 5,
			OccurredAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			OrderId = "order-stable",
			Amount = 100m,
		};

		// Act
		var bytes = serializer.SerializeEvent(evt);
		var json = Encoding.UTF8.GetString(bytes);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - verify the EXACT camelCase JSON keys produced by the serializer
		root.TryGetProperty("eventId", out _).ShouldBeTrue("Missing 'eventId' key");
		root.TryGetProperty("aggregateId", out _).ShouldBeTrue("Missing 'aggregateId' key");
		root.TryGetProperty("version", out _).ShouldBeTrue("Missing 'version' key");
		root.TryGetProperty("occurredAt", out _).ShouldBeTrue("Missing 'occurredAt' key");
		root.TryGetProperty("eventType", out _).ShouldBeTrue("Missing 'eventType' key");
		root.TryGetProperty("orderId", out _).ShouldBeTrue("Missing 'orderId' key");
		root.TryGetProperty("amount", out _).ShouldBeTrue("Missing 'amount' key");
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ProduceStableJsonKeys_ForEventWithJsonPropertyNameAttribute()
	{
		// Arrange
		var evt = new EventWithJsonPropertyName
		{
			EventId = "evt-attr",
			AggregateId = "agg-attr",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			CustomerId = "cust-attr",
		};

		// Act
		var bytes = serializer.SerializeEvent(evt);
		var json = Encoding.UTF8.GetString(bytes);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - JsonPropertyName("customerId") should produce "customerId"
		root.TryGetProperty("customerId", out var val).ShouldBeTrue("Missing 'customerId' key");
		val.GetString().ShouldBe("cust-attr");
	}

	#endregion

	#region Edge Cases

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void HandleEmptyStringProperties_WhenRoundTripping()
	{
		// Arrange
		var original = new SimpleContractEvent
		{
			EventId = string.Empty,
			AggregateId = string.Empty,
			Version = 0,
			OccurredAt = DateTimeOffset.MinValue,
			OrderId = string.Empty,
			Amount = 0m,
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert
		deserialized.EventId.ShouldBe(string.Empty);
		deserialized.AggregateId.ShouldBe(string.Empty);
		deserialized.OrderId.ShouldBe(string.Empty);
		deserialized.Amount.ShouldBe(0m);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void HandleMaxVersion_WhenRoundTripping()
	{
		// Arrange
		var original = new SimpleContractEvent
		{
			EventId = "evt-max",
			AggregateId = "agg-max",
			Version = long.MaxValue,
			OccurredAt = DateTimeOffset.UtcNow,
			OrderId = "order-max",
			Amount = decimal.MaxValue,
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert
		deserialized.Version.ShouldBe(long.MaxValue);
		deserialized.Amount.ShouldBe(decimal.MaxValue);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveDateTimeOffsetPrecision_WhenRoundTripping()
	{
		// Arrange
		var precise = new DateTimeOffset(2026, 3, 22, 10, 30, 45, 123, TimeSpan.FromHours(5));
		var original = new SimpleContractEvent
		{
			EventId = "evt-ts",
			AggregateId = "agg-ts",
			Version = 1,
			OccurredAt = precise,
			OrderId = "order-ts",
			Amount = 1m,
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert - DateTimeOffset should preserve offset and milliseconds
		deserialized.OccurredAt.ShouldBe(precise);
		deserialized.OccurredAt.Offset.ShouldBe(TimeSpan.FromHours(5));
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ThrowOnNullEvent_WhenSerializing()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.SerializeEvent(null!));
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ThrowOnNullData_WhenDeserializing()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeEvent(null!, typeof(SimpleContractEvent)));
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ThrowOnNullType_WhenDeserializing()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeEvent(new byte[] { 1, 2, 3 }, null!));
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ThrowOnInvalidJson_WhenDeserializing()
	{
		// Arrange
		var invalidBytes = Encoding.UTF8.GetBytes("not-valid-json");

		// Act & Assert
		Should.Throw<JsonException>(() =>
			serializer.DeserializeEvent(invalidBytes, typeof(SimpleContractEvent)));
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void ThrowOnNonEventType_WhenDeserializing()
	{
		// Arrange - serialize a valid object but try to deserialize as non-IDomainEvent type
		var json = """{"name": "test"}""";
		var bytes = Encoding.UTF8.GetBytes(json);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			serializer.DeserializeEvent(bytes, typeof(NonEventClass)));
	}

	#endregion

	#region Type Resolution Contract

	[Fact]
	public void ResolveType_ForKnownDomainEvent()
	{
		// Arrange
		var typeName = serializer.GetTypeName(typeof(SimpleContractEvent));

		// Act
		var resolved = serializer.ResolveType(typeName);

		// Assert
		resolved.ShouldBe(typeof(SimpleContractEvent));
	}

	[Fact]
	public void GetTypeName_ProducesConsistentName()
	{
		// Act
		var name1 = serializer.GetTypeName(typeof(SimpleContractEvent));
		var name2 = serializer.GetTypeName(typeof(SimpleContractEvent));

		// Assert
		name1.ShouldBe(name2);
		name1.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ThrowOnEmptyTypeName_WhenResolving()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => serializer.ResolveType(string.Empty));
	}

	[Fact]
	public void ThrowOnUnknownTypeName_WhenResolving()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			serializer.ResolveType("NonExistent.Type.That.Does.Not.Exist"));
	}

	#endregion

	#region DomainEvent Base Record Round-Trip

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveAllProperties_WhenRoundTrippingDomainEventRecord()
	{
		// Arrange - uses the DomainEvent abstract record base class
		var original = new OrderCreatedEvent("order-rec-1", 199.99m)
		{
			AggregateId = "order-rec-1",
			EventId = "evt-record",
			Version = 10,
			OccurredAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
		};

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (OrderCreatedEvent)serializer.DeserializeEvent(bytes, typeof(OrderCreatedEvent));

		// Assert
		deserialized.EventId.ShouldBe("evt-record");
		deserialized.AggregateId.ShouldBe("order-rec-1");
		deserialized.Version.ShouldBe(10);
		deserialized.OccurredAt.ShouldBe(original.OccurredAt);
		deserialized.EventType.ShouldBe(nameof(OrderCreatedEvent));
		deserialized.OrderId.ShouldBe("order-rec-1");
		deserialized.Total.ShouldBe(199.99m);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void PreserveMetadata_WhenRoundTrippingDomainEventRecord()
	{
		// Arrange
		var original = new OrderCreatedEvent("order-meta", 50m)
			.WithCorrelationId(Guid.Parse("12345678-1234-1234-1234-123456789012"))
			.WithCausationId("cause-001");

		// Act
		var bytes = serializer.SerializeEvent(original);
		var deserialized = (OrderCreatedEvent)serializer.DeserializeEvent(bytes, typeof(OrderCreatedEvent));

		// Assert -- T.21: CorrelationId/CausationId are first-class properties
		deserialized.CorrelationId.ShouldBe("12345678-1234-1234-1234-123456789012");
		deserialized.CausationId.ShouldBe("cause-001");
	}

	#endregion

	#region Backward Compatibility: Deserialize From Fixed JSON

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void DeserializeFromFixedJson_MatchingCurrentContract()
	{
		// Arrange - this JSON represents the "persisted" contract
		// If any property name changes in SimpleContractEvent, this test WILL break
		var fixedJson = """
			{
				"eventId": "evt-fixed",
				"aggregateId": "agg-fixed",
				"version": 7,
				"occurredAt": "2026-01-15T08:30:00+00:00",
				"eventType": "SimpleContractEvent",
				"orderId": "order-fixed",
				"amount": 42.50,
				"metadata": null
			}
			""";
		var bytes = Encoding.UTF8.GetBytes(fixedJson);

		// Act
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert - every property must match the fixed JSON values
		deserialized.EventId.ShouldBe("evt-fixed");
		deserialized.AggregateId.ShouldBe("agg-fixed");
		deserialized.Version.ShouldBe(7);
		deserialized.OccurredAt.ShouldBe(new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero));
		deserialized.EventType.ShouldBe("SimpleContractEvent");
		deserialized.OrderId.ShouldBe("order-fixed");
		deserialized.Amount.ShouldBe(42.50m);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
	public void DeserializeFromFixedJson_WithMetadata()
	{
		// Arrange
		var fixedJson = """
			{
				"eventId": "evt-meta-fixed",
				"aggregateId": "agg-meta-fixed",
				"version": 3,
				"occurredAt": "2026-02-20T14:00:00+00:00",
				"eventType": "SimpleContractEvent",
				"orderId": "order-meta-fixed",
				"amount": 100.00,
				"metadata": {
					"CorrelationId": "corr-fixed",
					"TenantId": "tenant-fixed"
				}
			}
			""";
		var bytes = Encoding.UTF8.GetBytes(fixedJson);

		// Act
		var deserialized = (SimpleContractEvent)serializer.DeserializeEvent(bytes, typeof(SimpleContractEvent));

		// Assert
		deserialized.Metadata.ShouldNotBeNull();
		deserialized.Metadata["CorrelationId"].ToString().ShouldBe("corr-fixed");
		deserialized.Metadata["TenantId"].ToString().ShouldBe("tenant-fixed");
	}

	#endregion

	#region Test Event Types

	/// <summary>
	/// Simple IDomainEvent implementation for contract testing.
	/// WARNING: Do NOT rename any properties -- existing tests validate the serialization contract.
	/// </summary>
	private sealed class SimpleContractEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; } = nameof(SimpleContractEvent);
		public IDictionary<string, object>? Metadata { get; init; }

		// Domain-specific properties
		public string OrderId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	/// <summary>
	/// Event with explicit JsonPropertyName to test attribute-based contract stability.
	/// </summary>
	private sealed class EventWithJsonPropertyName : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; } = nameof(EventWithJsonPropertyName);
		public IDictionary<string, object>? Metadata { get; init; }

		[JsonPropertyName("customerId")]
		public string? CustomerId { get; init; }
	}

	/// <summary>
	/// Simulates a BREAKING CHANGE: the property was renamed from "customer_id" to "client_id".
	/// Persisted events using the old name will lose data on deserialization.
	/// </summary>
	private sealed class EventWithRenamedProperty : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; } = nameof(EventWithRenamedProperty);
		public IDictionary<string, object>? Metadata { get; init; }

		// Was previously [JsonPropertyName("customer_id")] -- now changed to "client_id"
		[JsonPropertyName("client_id")]
		public string? ClientId { get; init; }
	}

	/// <summary>
	/// DomainEvent record using the abstract base class pattern.
	/// </summary>
	private sealed record OrderCreatedEvent(string OrderId, decimal Total) : DomainEvent;

	/// <summary>
	/// Non-IDomainEvent class for negative testing.
	/// </summary>
	private sealed class NonEventClass
	{
		public string? Name { get; init; }
	}

	#endregion
}
