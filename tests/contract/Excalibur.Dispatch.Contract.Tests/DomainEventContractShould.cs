// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Contract.Tests;

/// <summary>
/// Contract tests verifying IDomainEvent serialization schema remains
/// backward compatible. These tests ensure that event sourcing consumers
/// can safely deserialize domain events across versions.
/// </summary>
[Trait("Category", "Contract")]
[Trait("Component", "Dispatch.Core")]
public sealed class DomainEventContractShould
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	[Fact]
	public void Interface_HasExpectedMembers()
	{
		// Contract guard: IDomainEvent carries the members a message genuinely has — and deliberately
		// NOT a stream version/aggregate id (those live in the persistence envelope, not the messaging
		// contract). Re-adding AggregateId/Version re-opens the dispatch-vs-excalibur boundary leak.
		var properties = typeof(IDomainEvent).GetProperties();
		var propertyNames = properties.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

		propertyNames.ShouldContain("EventId");
		propertyNames.ShouldContain("OccurredAt");
		propertyNames.ShouldContain("EventType");
		propertyNames.ShouldContain("Metadata");
		propertyNames.ShouldNotContain("AggregateId", "AggregateId is a persistence concern, not a messaging one");
		propertyNames.ShouldNotContain("Version", "the stream version lives in the persistence envelope, not the event");
	}

	[Fact]
	public void Interface_InheritsFromIDispatchEvent()
	{
		// Contract: IDomainEvent must extend IDispatchEvent
		typeof(IDomainEvent).GetInterfaces().ShouldContain(typeof(IDispatchEvent));
	}

	[Fact]
	public void ConcreteEvent_Serialization_ProducesExpectedSchema()
	{
		// Arrange — use a concrete test implementation
		var domainEvent = new TestOrderPlacedEvent
		{
			EventId = "evt-001",
			AggregateId = "order-123",
			Version = 1,
			OccurredAt = new DateTimeOffset(2026, 3, 15, 14, 30, 0, TimeSpan.Zero),
			EventType = "OrderPlaced",
			Metadata = new Dictionary<string, object>
			{
				["userId"] = "user-42",
				["tenantId"] = "tenant-1",
			},
		};

		// Act
		var json = JsonSerializer.Serialize(domainEvent, SerializerOptions);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert — all IDomainEvent properties must be present in serialized form
		root.TryGetProperty("eventId", out _).ShouldBeTrue("IDomainEvent must serialize 'eventId'");
		root.TryGetProperty("aggregateId", out _).ShouldBeTrue("IDomainEvent must serialize 'aggregateId'");
		root.TryGetProperty("version", out _).ShouldBeTrue("IDomainEvent must serialize 'version'");
		root.TryGetProperty("occurredAt", out _).ShouldBeTrue("IDomainEvent must serialize 'occurredAt'");
		root.TryGetProperty("eventType", out _).ShouldBeTrue("IDomainEvent must serialize 'eventType'");
		root.TryGetProperty("metadata", out _).ShouldBeTrue("IDomainEvent must serialize 'metadata'");
	}

	[Fact]
	public void ConcreteEvent_Roundtrip_PreservesAllFields()
	{
		// Arrange
		var original = new TestOrderPlacedEvent
		{
			EventId = "evt-rt-001",
			AggregateId = "order-rt-456",
			Version = 5,
			OccurredAt = new DateTimeOffset(2026, 6, 20, 8, 0, 0, TimeSpan.Zero),
			EventType = "OrderPlaced",
			Metadata = new Dictionary<string, object>
			{
				["correlationId"] = "corr-rt-789",
			},
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<TestOrderPlacedEvent>(json, SerializerOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.EventId.ShouldBe(original.EventId);
		deserialized.AggregateId.ShouldBe(original.AggregateId);
		deserialized.Version.ShouldBe(original.Version);
		deserialized.EventType.ShouldBe(original.EventType);
	}

	[Fact]
	public void ConcreteEvent_WithNullMetadata_SerializesSuccessfully()
	{
		// Arrange
		var domainEvent = new TestOrderPlacedEvent
		{
			EventId = "evt-null-meta",
			AggregateId = "order-nm",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "OrderPlaced",
			Metadata = null,
		};

		// Act & Assert — null metadata should not break serialization
		var json = JsonSerializer.Serialize(domainEvent, SerializerOptions);
		json.ShouldNotBeNullOrEmpty();

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		root.TryGetProperty("metadata", out var metadataProp).ShouldBeTrue();
		metadataProp.ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public void PropertyTypes_MatchExpectedContract()
	{
		// Contract guard: property types must not change
		var props = typeof(IDomainEvent).GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);

		props["EventId"].ShouldBe(typeof(string));
		props["OccurredAt"].ShouldBe(typeof(DateTimeOffset));
		props["EventType"].ShouldBe(typeof(string));
		props["Metadata"].ShouldBe(typeof(IDictionary<string, object>));
		props.ShouldNotContainKey("AggregateId", "AggregateId was removed from the messaging contract");
		props.ShouldNotContainKey("Version", "Version was removed from the messaging contract");
	}

	/// <summary>
	/// Concrete test implementation of IDomainEvent for serialization testing.
	/// </summary>
	private sealed class TestOrderPlacedEvent : IDomainEvent
	{
		public string EventId { get; set; } = string.Empty;
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; }
		public string EventType { get; set; } = string.Empty;
		public IDictionary<string, object>? Metadata { get; set; }
	}
}