// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch;

/// <summary>
/// AOT-compatible JSON event serializer that uses compile-time type registries
/// instead of reflection-based type resolution.
/// </summary>
/// <remarks>
/// <para>
/// This serializer is the AOT-safe alternative to <see cref="JsonEventSerializer"/>.
/// It uses <see cref="IEventTypeRegistry"/> for type name resolution (populated from
/// the source-generated <c>EventStoreTypeMap</c>) and <see cref="JsonSerializerContext"/>
/// for type-safe JSON serialization.
/// </para>
/// <para>
/// Consumers opt in by referencing the <c>Excalibur.Dispatch.SourceGenerators</c> package,
/// which generates the type map at compile time.
/// </para>
/// <para>
/// <b>Wire-shape parity (enforced).</b> The emitted JSON is controlled entirely by the consumer-supplied
/// <see cref="JsonSerializerContext"/>. To stay byte-compatible with events written through the
/// reflection-based <see cref="JsonEventSerializer"/> (camelCase property names, enums as strings, nulls
/// omitted), the context MUST be annotated with
/// <c>[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
/// UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]</c>, and
/// the generated <c>Default</c> instance must be the one supplied — those settings are applied to
/// <c>MyContext.Default</c> and <em>not</em> to a <c>new MyContext()</c>, which carries default
/// (PascalCase, null-writing) options.
/// The naming-policy and null-handling halves of that contract are read back off
/// <see cref="JsonSerializerContext.Options"/>, and a divergent context is rejected by the constructor, so a
/// PascalCase or null-writing context fails loudly at composition instead of silently writing payloads the
/// reflection path mis-reads. The string-enum half is not surfaced on the options object and remains a
/// documented requirement.
/// </para>
/// <para>
/// <b>Event metadata.</b> <see cref="IDomainEvent.Metadata"/> is an <c>IDictionary&lt;string, object&gt;</c>,
/// so each metadata <em>value</em> is written as its runtime type. Source generation resolves nothing at run
/// time, so every runtime value type the application places in that dictionary must itself be declared on the
/// context — <c>[JsonSerializable(typeof(string))]</c>, <c>[JsonSerializable(typeof(int))]</c>,
/// <c>[JsonSerializable(typeof(bool))]</c>, and so on for the types actually stored. An undeclared value type
/// raises <see cref="NotSupportedException"/> on serialize. Declare the closed value types; do not declare
/// <c>Dictionary&lt;string, object&gt;</c> itself as a shortcut — that compiles and then throws at run time on
/// the same values.
/// </para>
/// </remarks>
public sealed class AotJsonEventSerializer : IEventSerializer
{
	private readonly IEventTypeRegistry _typeRegistry;
	private readonly JsonSerializerContext _jsonContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="AotJsonEventSerializer"/> class over an explicit set of
	/// event types.
	/// </summary>
	/// <param name="jsonContext">
	/// The source-generated JSON serializer context declaring every event type — and every metadata value
	/// type — the application serializes. Pass the generated <c>Default</c> instance.
	/// </param>
	/// <param name="eventTypes">
	/// The event types resolvable by name when loading stored events. These are the same types passed to
	/// <c>AddEventTypes(...)</c>; an event type that is absent here cannot be loaded back from the store.
	/// </param>
	/// <remarks>
	/// This is the manual-composition entry point. Applications using dependency injection call
	/// <c>services.AddAotEventSerializer(MyJsonContext.Default)</c> instead, which shares the event-type
	/// allow-list already populated by <c>AddEventTypes(...)</c>.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="jsonContext"/> or <paramref name="eventTypes"/> (or any element) is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="jsonContext"/> does not carry the canonical event wire-shape settings.
	/// </exception>
	public AotJsonEventSerializer(JsonSerializerContext jsonContext, params Type[] eventTypes)
		: this(BuildRegistry(eventTypes), jsonContext)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AotJsonEventSerializer"/> class.
	/// </summary>
	/// <param name="typeRegistry">The compile-time event type registry.</param>
	/// <param name="jsonContext">The source-generated JSON serializer context containing event type metadata.</param>
	internal AotJsonEventSerializer(IEventTypeRegistry typeRegistry, JsonSerializerContext jsonContext)
	{
		ArgumentNullException.ThrowIfNull(typeRegistry);
		ArgumentNullException.ThrowIfNull(jsonContext);

		VerifyWireShapeParity(jsonContext);

		_typeRegistry = typeRegistry;
		_jsonContext = jsonContext;
	}

	private static EventTypeRegistry BuildRegistry(Type[] eventTypes)
	{
		ArgumentNullException.ThrowIfNull(eventTypes);

		var registry = new EventTypeRegistry();
		foreach (var eventType in eventTypes)
		{
			ArgumentNullException.ThrowIfNull(eventType, nameof(eventTypes));
			registry.Register(eventType);
		}

		return registry;
	}

	// The stored wire format is fixed by the reflection serializer's canonical options. A source-generated
	// context carries its [JsonSourceGenerationOptions] settings through to Options, so the naming-policy and
	// null-handling halves of the contract are checkable here rather than left to guidance; a context that
	// diverges writes payloads the reflection path silently mis-reads (PascalCase keys, explicit nulls), which
	// is unrecoverable once events are stored. Reject it at composition instead.
	private static void VerifyWireShapeParity(JsonSerializerContext jsonContext)
	{
		const string Remedy =
			"Annotate the context with [JsonSourceGenerationOptions(PropertyNamingPolicy = " +
			"JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, DefaultIgnoreCondition = " +
			"JsonIgnoreCondition.WhenWritingNull)], and pass its generated Default instance — those " +
			"settings are applied to MyContext.Default and not to a new MyContext().";

		var options = jsonContext.Options;

		if (options.PropertyNamingPolicy != JsonNamingPolicy.CamelCase)
		{
			throw new ArgumentException(
				$"The JsonSerializerContext '{jsonContext.GetType().FullName}' does not use the camelCase " +
				"property naming policy, so it would write event payloads that the reflection-based event " +
				"serializer cannot read back. " + Remedy,
				nameof(jsonContext));
		}

		if (options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingNull)
		{
			throw new ArgumentException(
				$"The JsonSerializerContext '{jsonContext.GetType().FullName}' does not omit null values, so " +
				"it would write event payloads that differ from those written by the reflection-based event " +
				"serializer. " + Remedy,
				nameof(jsonContext));
		}
	}

	/// <inheritdoc />
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		// surface failures as SerializationException (canonical contract) so event-store
		// read/write failures are uniformly catchable/poison-routable, like SpanEventSerializer.
		var eventType = domainEvent.GetType();
		try
		{
			var typeInfo = _jsonContext.GetTypeInfo(eventType)
				?? throw new SerializationException(
					$"No JsonTypeInfo found for event type '{eventType.FullName}'. " +
					"Ensure the type is registered in your JsonSerializerContext with [JsonSerializable(typeof(T))].");

			return JsonSerializer.SerializeToUtf8Bytes(domainEvent, typeInfo);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(eventType, "serialize", ex);
		}
	}

	/// <inheritdoc />
	public IDomainEvent DeserializeEvent(byte[] data, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(eventType);

		// surface failures as SerializationException (canonical contract) so event-store
		// read failures are uniformly catchable/poison-routable, like SpanEventSerializer.
		try
		{
			var typeInfo = _jsonContext.GetTypeInfo(eventType)
				?? throw new SerializationException(
					$"No JsonTypeInfo found for event type '{eventType.FullName}'. " +
					"Ensure the type is registered in your JsonSerializerContext with [JsonSerializable(typeof(T))].");

			var @event = JsonSerializer.Deserialize(data.AsSpan(), typeInfo);

			return @event as IDomainEvent ??
				   throw new SerializationException($"Deserialized object is not an IDomainEvent: {eventType}");
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(eventType, "deserialize", ex);
		}
	}

	/// <inheritdoc />
	public string GetTypeName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		// The declared name, and only the declared name -- the same derivation the other serializer
		// uses. This previously consulted the registry first and fell back to the helper, which made
		// two serializers answer one contract two different ways. The registry's type-to-name map is
		// populated from exactly this helper and never from an alias, so the lookup could only ever
		// return the same string or nothing; what it actually bought was an unspecified difference a
		// caller could observe between two implementations of IEventSerializer.
		return MessageNameHelper.GetName(type);
	}

	/// <inheritdoc />
	public Type ResolveType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			throw new ArgumentException("Type name cannot be null or empty.", nameof(typeName));
		}

		return _typeRegistry.ResolveType(typeName)
			?? throw new UnknownEventTypeException(
				$"Cannot resolve event type '{typeName}'. No registered type declares that name. " +
				"Register the type with AddEventTypes<T>(), and check that its [MessageName] matches " +
				"the stored name -- if the name changed, the old one has to be kept with " +
				"[MessageNameAlias] for data written under it to stay readable.");
	}
}
