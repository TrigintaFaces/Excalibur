// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
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
/// </remarks>
internal sealed class AotJsonEventSerializer : IEventSerializer
{
	private readonly IEventTypeRegistry _typeRegistry;
	private readonly JsonSerializerContext _jsonContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="AotJsonEventSerializer"/> class.
	/// </summary>
	/// <param name="typeRegistry">The compile-time event type registry.</param>
	/// <param name="jsonContext">The source-generated JSON serializer context containing event type metadata.</param>
	public AotJsonEventSerializer(IEventTypeRegistry typeRegistry, JsonSerializerContext jsonContext)
	{
		ArgumentNullException.ThrowIfNull(typeRegistry);
		ArgumentNullException.ThrowIfNull(jsonContext);

		_typeRegistry = typeRegistry;
		_jsonContext = jsonContext;
	}

	/// <inheritdoc />
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		// ifgj5w: surface failures as SerializationException (canonical contract) so event-store
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

		// ifgj5w: surface failures as SerializationException (canonical contract) so event-store
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

		return _typeRegistry.GetTypeName(type)
			?? EventTypeNameHelper.GetEventTypeName(type);
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
				$"Cannot resolve event type '{typeName}'. Ensure the type is registered in your " +
				"EventStoreTypeMap via the [DomainEvent] attribute or explicit registration.");
	}
}
