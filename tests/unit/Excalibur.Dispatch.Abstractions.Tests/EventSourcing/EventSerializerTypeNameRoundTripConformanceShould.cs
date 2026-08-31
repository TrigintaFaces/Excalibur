// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Liskov L9 (uo90tv) — the load-bearing behavioural-subtype postcondition for the
/// <see cref="IEventSerializer"/> type-resolution seam: for any type the serializer can resolve, the name
/// produced by <see cref="IEventSerializer.GetTypeName"/> MUST resolve back to the SAME type via
/// <see cref="IEventSerializer.ResolveType"/> — <c>ResolveType(GetTypeName(t)) == t</c> (round-trip
/// identity). This is the substitutability contract every <see cref="IEventSerializer"/> implementor
/// (Json / Aot / Span) must honour; a store that persists an event under the name from GetTypeName and
/// later reads it back through ResolveType relies on this identity holding, or event replay resolves the
/// WRONG type (or fails).
/// </summary>
/// <remarks>
/// <para>
/// <b>Postcondition, not mechanism (testing-patterns §3).</b> The base asserts the <i>property</i>
/// (the name round-trips to the same CLR type), never a particular naming scheme — an implementor is free
/// to use an assembly-qualified name, a registry short-name, or any stable token, as long as its own
/// ResolveType inverts its own GetTypeName. Wire-parity across implementors is a separate concern; here we
/// pin the per-implementor identity that Liskov substitutability requires.
/// </para>
/// <para>
/// <b>Non-vacuity is proven two ways.</b> (1) A real implementor deriver (AotJsonEventSerializer) runs the
/// postcondition GREEN against the shipped code. (2) The sibling
/// <see cref="EventSerializerTypeNameRoundTripNonVacuityShould"/> proves the assertion is <i>discriminating</i>
/// — it RED-fails against a hand-written, direct-<see cref="IEventSerializer"/> fixture whose GetTypeName and
/// ResolveType disagree (an AOT-casing corruption), and GREEN-passes against a correct hand-written fixture.
/// The direct fixtures implement the interface from scratch (only <see cref="IEventSerializer"/> in the base
/// list), so the lock binds the <i>interface's</i> requirement, not an inherited base's convenience
/// (fixture-shape corollary).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public abstract class EventSerializerTypeNameRoundTripConformanceTestBase
{
	/// <summary>
	/// The representative event types the round-trip identity postcondition must hold for. Each is a distinct
	/// <see cref="IDomainEvent"/> CLR type; an implementor that collapses or corrupts names would fail to
	/// invert at least one of them.
	/// </summary>
	protected static IReadOnlyList<Type> RoundTripTypes { get; } =
	[
		typeof(ConformanceOrderPlaced),
		typeof(ConformancePaymentReceived),
	];

	/// <summary>
	/// Creates the <see cref="IEventSerializer"/> under test, configured so that every type in
	/// <paramref name="resolvableTypes"/> is resolvable through its <see cref="IEventSerializer.ResolveType"/>
	/// path (registered in the type map / allow-list). A serializer that cannot resolve a type it just named
	/// is exactly the Liskov violation this postcondition forbids.
	/// </summary>
	protected abstract IEventSerializer CreateSerializer(IReadOnlyList<Type> resolvableTypes);

	[Fact]
	public void ResolveType_OfGetTypeName_ReturnsTheSameType_RoundTripIdentity()
	{
		// Arrange
		var serializer = CreateSerializer(RoundTripTypes);

		foreach (var type in RoundTripTypes)
		{
			// Act — name the type, then resolve the name back.
			var name = serializer.GetTypeName(type);

			// LIVENESS: the serializer actually produces a usable name (not null/empty) AND resolves it back to
			// a real type — a serializer that returned nothing / refused everything would fail HERE, not pass
			// vacuously.
			name.ShouldNotBeNullOrEmpty(
				$"GetTypeName must produce a persistable name for '{type.FullName}'");

			var resolved = serializer.ResolveType(name);

			// SAFETY + identity: the resolved type is the SAME type — never a different type, never a silent
			// mismatch. This is the behavioural-subtype postcondition.
			resolved.ShouldBe(
				type,
				$"ResolveType(GetTypeName(t)) must round-trip to the same type — expected '{type.FullName}', " +
				$"got '{resolved.FullName}' via name '{name}'");
		}
	}
}

/// <summary>
/// L9 real-implementor deriver: the shipped <see cref="AotJsonEventSerializer"/> honours the round-trip
/// identity for registered types (GetTypeName → registry name, ResolveType → registry lookup).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class AotJsonEventSerializerTypeNameRoundTripShould : EventSerializerTypeNameRoundTripConformanceTestBase
{
	protected override IEventSerializer CreateSerializer(IReadOnlyList<Type> resolvableTypes)
	{
		var registry = new ConformanceTypeRegistry();
		foreach (var type in resolvableTypes)
		{
			// Register under the canonical persisted name so GetTypeName (registry hit) and ResolveType agree.
			registry.Register(type.FullName!, type);
		}

		return new AotJsonEventSerializer(registry, ConformanceJsonContext.Default);
	}
}

/// <summary>
/// Proves the L9 round-trip-identity postcondition is NON-VACUOUS: it RED-fails against a direct-interface
/// implementor whose GetTypeName / ResolveType disagree, and GREEN-passes against a correct one. Both
/// fixtures implement <see cref="IEventSerializer"/> from scratch (no first-party base), so the lock binds
/// the interface contract, not an inherited convenience.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class EventSerializerTypeNameRoundTripNonVacuityShould
{
	[Fact]
	public void RoundTripIdentity_IsViolated_ByACasingCorruptingSerializer()
	{
		// Arrange — a serializer whose GetTypeName upper-cases the name but whose ResolveType only knows the
		// exact-cased registered name (the AOT-casing divergence L9 targets). This is the shape of a real
		// implementor that names types one way and resolves them another.
		var corrupting = new CasingCorruptingEventSerializer();
		var type = typeof(ConformanceOrderPlaced);

		var name = corrupting.GetTypeName(type);

		// The postcondition (ResolveType(GetTypeName(t)) == t) MUST fail here — the corrupted name cannot be
		// inverted. Proven RED: resolution throws because the upper-cased name is not in the allow-list.
		_ = Should.Throw<UnknownEventTypeException>(() => corrupting.ResolveType(name));
	}

	[Fact]
	public void RoundTripIdentity_Holds_ForACorrectDirectSerializer()
	{
		// Arrange — a minimal correct implementor: GetTypeName and ResolveType use the SAME canonical token.
		var correct = new IdentityEventSerializer();
		var type = typeof(ConformancePaymentReceived);

		// Act
		var resolved = correct.ResolveType(correct.GetTypeName(type));

		// Assert — GREEN: the correct implementor round-trips.
		resolved.ShouldBe(type);
	}
}

/// <summary>Minimal in-memory <see cref="IEventTypeRegistry"/> for the conformance derivers.</summary>
internal sealed class ConformanceTypeRegistry : IEventTypeRegistry
{
	private readonly Dictionary<string, Type> _nameToType = new(StringComparer.Ordinal);
	private readonly Dictionary<Type, string> _typeToName = [];

	public void Register(string name, Type type)
	{
		_nameToType[name] = type;
		_typeToName[type] = name;
	}

	public Type? ResolveType(string eventTypeName) => _nameToType.GetValueOrDefault(eventTypeName);

	public string? GetTypeName(Type eventType) => _typeToName.GetValueOrDefault(eventType);
}

/// <summary>AOT source-gen context for the conformance event types (construction requirement only).</summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConformanceOrderPlaced))]
[JsonSerializable(typeof(ConformancePaymentReceived))]
internal sealed partial class ConformanceJsonContext : JsonSerializerContext;

/// <summary>
/// Direct-<see cref="IEventSerializer"/> VIOLATING fixture: GetTypeName upper-cases the canonical name while
/// ResolveType only recognises the exact-cased allow-list entry, so <c>ResolveType(GetTypeName(t))</c> cannot
/// invert. Implements the interface from scratch (no first-party base).
/// </summary>
internal sealed class CasingCorruptingEventSerializer : IEventSerializer
{
	private static readonly IReadOnlyDictionary<string, Type> AllowList = new Dictionary<string, Type>(StringComparer.Ordinal)
	{
		[EventTypeNameHelper.GetEventTypeName(typeof(ConformanceOrderPlaced))] =
			typeof(ConformanceOrderPlaced),
	};

	public byte[] SerializeEvent(IDomainEvent domainEvent) => throw new NotSupportedException();

	public IDomainEvent DeserializeEvent(byte[] data, Type eventType) => throw new NotSupportedException();

	// Corruption: produces an UPPER-CASED name that the allow-list (exact-cased) can never match.
	public string GetTypeName(Type type) =>
		EventTypeNameHelper.GetEventTypeName(type).ToUpperInvariant();

	public Type ResolveType(string typeName) =>
		AllowList.TryGetValue(typeName, out var type)
			? type
			: throw new UnknownEventTypeException($"Cannot resolve event type '{typeName}'.");
}

/// <summary>
/// Direct-<see cref="IEventSerializer"/> CORRECT fixture: GetTypeName and ResolveType use the SAME canonical
/// token, so the round-trip identity holds. Implements the interface from scratch (no first-party base).
/// </summary>
internal sealed class IdentityEventSerializer : IEventSerializer
{
	private static readonly IReadOnlyDictionary<string, Type> AllowList = new Dictionary<string, Type>(StringComparer.Ordinal)
	{
		[EventTypeNameHelper.GetEventTypeName(typeof(ConformancePaymentReceived))] =
			typeof(ConformancePaymentReceived),
	};

	public byte[] SerializeEvent(IDomainEvent domainEvent) => throw new NotSupportedException();

	public IDomainEvent DeserializeEvent(byte[] data, Type eventType) => throw new NotSupportedException();

	public string GetTypeName(Type type) => EventTypeNameHelper.GetEventTypeName(type);

	public Type ResolveType(string typeName) =>
		AllowList.TryGetValue(typeName, out var type)
			? type
			: throw new UnknownEventTypeException($"Cannot resolve event type '{typeName}'.");
}

/// <summary>Representative domain event for the type-name round-trip conformance suite.</summary>
internal sealed class ConformanceOrderPlaced : IDomainEvent
{
	public string OrderId { get; set; } = string.Empty;

	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(ConformanceOrderPlaced);

	public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>Second representative domain event for the type-name round-trip conformance suite.</summary>
internal sealed class ConformancePaymentReceived : IDomainEvent
{
	public decimal Amount { get; set; }

	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(ConformancePaymentReceived);

	public IDictionary<string, object>? Metadata { get; set; }
}
