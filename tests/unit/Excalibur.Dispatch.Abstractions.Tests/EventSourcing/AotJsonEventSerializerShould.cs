// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.EventSourcing;

/// <summary>
/// Tests for <see cref="AotJsonEventSerializer"/> covering AOT-safe event serialization,
/// type registry integration, and error handling.
/// Sprint 737 T.21: Wave 3 AOT tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "AOT")]
public sealed partial class AotJsonEventSerializerShould
{
	private readonly TestTypeRegistry _registry = new();
	private readonly AotTestJsonContext _jsonContext = new();

	[Fact]
	public void ThrowForNullTypeRegistry()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AotJsonEventSerializer(null!, _jsonContext));
	}

	[Fact]
	public void ThrowForNullJsonContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AotJsonEventSerializer(_registry, null!));
	}

	[Fact]
	public void SerializeAndDeserializeRoundTrip()
	{
		_registry.Register<TestOrderCreated>("TestOrderCreated");
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var original = new TestOrderCreated
		{
			OrderId = "ORD-123",
			Total = 99.95m,
			EventId = "evt-1",
			AggregateId = "agg-1",
			Version = 1,
			OccurredAt = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
			EventType = "TestOrderCreated",
		};

		var bytes = sut.SerializeEvent(original);
		var deserialized = sut.DeserializeEvent(bytes, typeof(TestOrderCreated));

		var result = deserialized.ShouldBeOfType<TestOrderCreated>();
		result.OrderId.ShouldBe("ORD-123");
		result.Total.ShouldBe(99.95m);
		result.EventId.ShouldBe("evt-1");
		result.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public void SerializeProducesValidUtf8Json()
	{
		_registry.Register<TestOrderCreated>("TestOrderCreated");
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var @event = new TestOrderCreated { OrderId = "ORD-1", Total = 10m };
		var bytes = sut.SerializeEvent(@event);

		var json = Encoding.UTF8.GetString(bytes);
		json.ShouldContain("ORD-1");

		// Verify it's valid JSON
		using var doc = JsonDocument.Parse(json);
		doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
	}

	[Fact]
	public void ThrowForNullDomainEventOnSerialize()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		Should.Throw<ArgumentNullException>(() => sut.SerializeEvent(null!));
	}

	[Fact]
	public void ThrowForNullDataOnDeserialize()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		Should.Throw<ArgumentNullException>(() => sut.DeserializeEvent(null!, typeof(TestOrderCreated)));
	}

	[Fact]
	public void ThrowForNullTypeOnDeserialize()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		Should.Throw<ArgumentNullException>(() => sut.DeserializeEvent([]!, null!));
	}

	[Fact]
	public void ThrowWhenTypeNotInJsonContext()
	{
		// UnregisteredEvent is NOT in AotTestJsonContext
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);
		var @event = new UnregisteredEvent();

		var ex = Should.Throw<InvalidOperationException>(() => sut.SerializeEvent(@event));
		ex.Message.ShouldContain("No JsonTypeInfo found");
		ex.Message.ShouldContain("UnregisteredEvent");
	}

	[Fact]
	public void ResolveTypeFromRegistry()
	{
		_registry.Register<TestOrderCreated>("TestOrderCreated");
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var resolved = sut.ResolveType("TestOrderCreated");
		resolved.ShouldBe(typeof(TestOrderCreated));
	}

	[Fact]
	public void ThrowWhenTypeNameNotInRegistry()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var ex = Should.Throw<InvalidOperationException>(() => sut.ResolveType("NonExistent.Event"));
		ex.Message.ShouldContain("Cannot resolve event type");
		ex.Message.ShouldContain("NonExistent.Event");
	}

	[Fact]
	public void ThrowForNullOrEmptyTypeName()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		Should.Throw<ArgumentException>(() => sut.ResolveType(null!));
		Should.Throw<ArgumentException>(() => sut.ResolveType(string.Empty));
	}

	[Fact]
	public void GetTypeNameFromRegistry()
	{
		_registry.Register<TestOrderCreated>("TestOrderCreated");
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var name = sut.GetTypeName(typeof(TestOrderCreated));
		name.ShouldBe("TestOrderCreated");
	}

	[Fact]
	public void FallBackToEventTypeNameHelperWhenNotInRegistry()
	{
		// Don't register the type -- should fall back to EventTypeNameHelper
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		var name = sut.GetTypeName(typeof(TestOrderCreated));

		// EventTypeNameHelper returns AssemblyQualifiedName or FullName
		name.ShouldContain("TestOrderCreated");
	}

	[Fact]
	public void ThrowForNullTypeOnGetTypeName()
	{
		var sut = new AotJsonEventSerializer(_registry, _jsonContext);

		Should.Throw<ArgumentNullException>(() => sut.GetTypeName(null!));
	}

	// --- Test helpers ---

	internal sealed class TestOrderCreated : IDomainEvent
	{
		public string OrderId { get; set; } = string.Empty;
		public decimal Total { get; set; }
		public string EventId { get; set; } = Guid.NewGuid().ToString();
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
		public string EventType { get; set; } = nameof(TestOrderCreated);
		public IDictionary<string, object>? Metadata { get; set; }
	}

	internal sealed class UnregisteredEvent : IDomainEvent
	{
		public string EventId { get; set; } = Guid.NewGuid().ToString();
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
		public string EventType { get; set; } = "UnregisteredEvent";
		public IDictionary<string, object>? Metadata { get; set; }
	}

	/// <summary>
	/// Simple in-memory type registry for testing.
	/// </summary>
	private sealed class TestTypeRegistry : IEventTypeRegistry
	{
		private readonly Dictionary<string, Type> _nameToType = new(StringComparer.Ordinal);
		private readonly Dictionary<Type, string> _typeToName = [];

		public void Register<T>(string name)
		{
			_nameToType[name] = typeof(T);
			_typeToName[typeof(T)] = name;
		}

		public Type? ResolveType(string eventTypeName) =>
			_nameToType.GetValueOrDefault(eventTypeName);

		public string? GetTypeName(Type eventType) =>
			_typeToName.GetValueOrDefault(eventType);
	}

	[JsonSerializable(typeof(TestOrderCreated))]
	private sealed partial class AotTestJsonContext : JsonSerializerContext;
}
