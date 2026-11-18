// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		propertyNames.ShouldContain("Metadata");
		propertyNames.ShouldNotContain(
			"EventType",
			"identity is declared on the type with [MessageName], not carried per-instance -- a property " +
			"let each instance name itself, so what a subscriber matched on depended on what that " +
			"instance happened to set");
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

		// Assert — every IDomainEvent property must be present in serialized form.
		root.TryGetProperty("eventId", out _).ShouldBeTrue("IDomainEvent must serialize 'eventId'");
		root.TryGetProperty("occurredAt", out _).ShouldBeTrue("IDomainEvent must serialize 'occurredAt'");
		root.TryGetProperty("metadata", out _).ShouldBeTrue("IDomainEvent must serialize 'metadata'");

		// aggregateId and version are this TEST TYPE's own properties, not the interface's -- they are
		// asserted here to show a concrete event may still carry them, not because the contract does.
		root.TryGetProperty("aggregateId", out _).ShouldBeTrue("the test event declares its own aggregateId");
		root.TryGetProperty("version", out _).ShouldBeTrue("the test event declares its own version");

		// eventType must NOT appear: the stored and transmitted name is declared on the type with
		// [MessageName] and resolved from it, so an event no longer carries a per-instance name that
		// could disagree with the one subscribers filter on.
		root.TryGetProperty("eventType", out _)
			.ShouldBeFalse("identity comes from [MessageName], not a serialized per-instance property");
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
		props["Metadata"].ShouldBe(typeof(IDictionary<string, object>));
		props.ShouldNotContainKey("EventType", "the stored/transmitted name comes from [MessageName]");
		props.ShouldNotContainKey("AggregateId", "AggregateId was removed from the messaging contract");
		props.ShouldNotContainKey("Version", "Version was removed from the messaging contract");
	}

	/// <summary>
	/// Concrete test implementation of IDomainEvent for serialization testing.
	/// </summary>
	[MessageName("Test.TestOrderPlacedEvent")]
	private sealed class TestOrderPlacedEvent : IDomainEvent
	{
		public string EventId { get; set; } = string.Empty;
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; }
		public IDictionary<string, object>? Metadata { get; set; }
	}

	// ---------------------------------------------------------------------------------------------
	// EventId must be CARRIED ON THE WIRE, and the fixtures below derive DomainEvent rather than
	// implementing IDomainEvent directly -- which the other fixture in this file does, and which is
	// why those arms cannot see this. The requirement lives on the record's property, so only a
	// DomainEvent-derived type reaches it.
	//
	// What shipped: EventId is a non-nullable string with a property initializer, and a property
	// initializer does NOT fire when a deserialized payload omits the field. So an omitted eventId
	// produced a non-nullable string holding NULL, which the inbox's first identity arm then skipped
	// (it guards on IsNullOrEmpty), silently demoting the event's own identity to whatever transport
	// source answered next -- or to none.
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// SAFETY: a payload that omits eventId is REFUSED rather than yielding a null identity.
	/// </summary>
	[Fact]
	public void RefuseAPayloadThatOmitsEventId()
	{
		const string omitted = "{\"orderNumber\":\"A-1\"}";

		var thrown = Should.Throw<JsonException>(
			() => JsonSerializer.Deserialize<WireContractEvent>(omitted, SerializerOptions),
			"an omitted eventId used to deserialize successfully and leave a non-nullable string holding "
			+ "null. Refusing the payload is the only outcome that cannot be mistaken for a real identity.");

		thrown.Message.ShouldContain(
			"eventId",
			Case.Insensitive,
			"the refusal must name the missing field, or an operator cannot tell which member was absent.");
	}

	/// <summary>
	/// LIVENESS: a payload that carries eventId still round-trips, and the value is the producer's.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by a type that refuses every payload.
	/// </remarks>
	[Fact]
	public void RoundTripAPayloadThatCarriesEventId()
	{
		const string supplied = "{\"eventId\":\"e-1\",\"orderNumber\":\"A-1\"}";

		var restored = JsonSerializer.Deserialize<WireContractEvent>(supplied, SerializerOptions);

		_ = restored.ShouldNotBeNull();
		restored.EventId.ShouldBe("e-1", "the identity on the wire is the producer's and must survive intact.");
		restored.OrderNumber.ShouldBe("A-1");
	}

	/// <summary>
	/// LIVENESS, and the arm that keeps the fix from becoming a breaking change to construction:
	/// building an event in process still mints an identity without the caller supplying one.
	/// </summary>
	/// <remarks>
	/// This is why the requirement is a serialization-time one rather than the C# <c>required</c>
	/// modifier. <c>required</c> would have closed the same hole and forced EVERY consumer
	/// construction site to pass an EventId, losing the generated identity they get for free today.
	/// If someone later "simplifies" this to <c>required</c>, this arm fails to compile -- which is
	/// the intended alarm.
	/// </remarks>
	[Fact]
	public void StillMintAnIdentityWhenConstructedInProcess()
	{
		var constructed = new WireContractEvent { OrderNumber = "A-1" };

		constructed.EventId.ShouldNotBeNullOrWhiteSpace(
			"constructing an event in process must not require the caller to invent an identity.");
	}

	/// <summary>
	/// A concrete event over the real <see cref="DomainEvent" /> record, as a consumer would write one.
	/// </summary>
	private sealed record WireContractEvent : DomainEvent
	{
		public string? OrderNumber { get; init; }
	}
}