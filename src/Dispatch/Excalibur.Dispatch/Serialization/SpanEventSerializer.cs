// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Event serializer implementation using the pluggable serialization infrastructure.
/// </summary>
/// <remarks>
/// This serializer implements <see cref="IEventSerializer"/> by delegating to the
/// configured <see cref="ISerializer"/> (the current/default serializer, typically JSON).
/// </remarks>
public sealed class SpanEventSerializer : IEventSerializer
{
	/// <summary>
	/// Safety margin added to size estimates to handle serializer overhead variations.
	/// </summary>
	private const int SizeMargin = 64;

	private readonly ISerializer _pluggable;

	/// <summary>
	/// Initializes a new instance of <see cref="SpanEventSerializer"/> using the specified
	/// pluggable serializer.
	/// </summary>
	/// <param name="pluggable">The underlying pluggable serializer (typically MemoryPack).</param>
	/// <exception cref="ArgumentNullException">Thrown when pluggable is null.</exception>
	public SpanEventSerializer(ISerializer pluggable)
	{
		_pluggable = pluggable ?? throw new ArgumentNullException(nameof(pluggable));
	}

	/// <summary>
	/// Initializes a new instance of <see cref="SpanEventSerializer"/> using the registry
	/// to obtain the MemoryPack serializer.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <exception cref="ArgumentNullException">Thrown when registry is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no serializer is available.
	/// </exception>
	public SpanEventSerializer(ISerializerRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		// Prefer the current/default serializer (JSON-first ),
		// fall back to MemoryPack only if no current serializer is configured
		_pluggable = registry.GetCurrent().Serializer
					 ?? registry.GetByName("MemoryPack")
					 ?? registry.GetById(SerializerIds.MemoryPack)
					 ?? throw new InvalidOperationException(
						"No serializer available. Configure a default serializer via AddPluggableSerialization().");
	}

	#region Span-based methods

	/// <summary>
	/// Deserializes an event from a read-only span (zero-copy).
	/// </summary>
	[RequiresDynamicCode("Deserialization of events requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Deserialization may reference types not preserved during trimming")]
	public IDomainEvent DeserializeEvent(ReadOnlySpan<byte> data, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(eventType);

		var result = _pluggable.DeserializeObject(data, eventType);

		if (result is not IDomainEvent domainEvent)
		{
			throw new SerializationException(
				$"Deserialized object is not an IDomainEvent. Got: {result?.GetType().Name ?? "null"}");
		}

		return domainEvent;
	}

	/// <summary>
	/// Serializes a snapshot to a caller-provided span buffer.
	/// </summary>
	[RequiresDynamicCode("Serialization of snapshots requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Serialization may reference types not preserved during trimming")]
	public int SerializeSnapshot(object snapshot, Span<byte> buffer)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var snapshotType = snapshot.GetType();
		var bytes = _pluggable.SerializeObject(snapshot, snapshotType);

		if (bytes.Length > buffer.Length)
		{
			throw new ArgumentException(
				$"Buffer too small. Required: {bytes.Length}, Available: {buffer.Length}. " +
				$"Use GetSnapshotSize() to determine required buffer size.",
				nameof(buffer));
		}

		bytes.CopyTo(buffer);
		return bytes.Length;
	}

	/// <summary>
	/// Deserializes a snapshot from a read-only span (zero-copy).
	/// </summary>
	[RequiresDynamicCode("Deserialization of snapshots requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Deserialization may reference types not preserved during trimming")]
	public object DeserializeSnapshot(ReadOnlySpan<byte> data, Type snapshotType)
	{
		ArgumentNullException.ThrowIfNull(snapshotType);

		return _pluggable.DeserializeObject(data, snapshotType);
	}

	/// <summary>
	/// Gets the required buffer size for serializing a snapshot.
	/// </summary>
	[RequiresDynamicCode("Size calculation may require dynamic code generation")]
	[RequiresUnreferencedCode("Size calculation may reference types not preserved during trimming")]
	public int GetSnapshotSize(object snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		// Serialize once to get exact size, then add margin
		var snapshotType = snapshot.GetType();
		var bytes = _pluggable.SerializeObject(snapshot, snapshotType);
		return bytes.Length + SizeMargin;
	}

	#endregion Span-based methods

	#region IEventSerializer - byte[] and type resolution methods

	/// <inheritdoc />
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var eventType = domainEvent.GetType();
		return _pluggable.SerializeObject(domainEvent, eventType);
	}

	/// <inheritdoc />
	public IDomainEvent DeserializeEvent(byte[] data, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(eventType);

		var result = _pluggable.DeserializeObject(data, eventType);

		if (result is not IDomainEvent domainEvent)
		{
			throw new SerializationException(
				$"Deserialized object is not an IDomainEvent. Got: {result?.GetType().Name ?? "null"}");
		}

		return domainEvent;
	}

	/// <inheritdoc />
	public string GetTypeName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
	}

	/// <inheritdoc />
	public Type ResolveType(string typeName)
	{
		ArgumentNullException.ThrowIfNull(typeName);

		// S-E: resolve ONLY via the registered allow-list (no unbounded AppDomain.GetAssemblies()
		// scan), so an unregistered/attacker-chosen type name cannot be deserialized — the gadget-chain
		// vector is inexpressible. Registered types resolve identically under JIT and AOT.
		if (TypeResolution.TypeResolverRegistry.TryResolveType(typeName, out var type) && type is not null)
		{
			return type;
		}

		throw new UnknownEventTypeException(
			$"Cannot resolve event type '{typeName}': it is not registered. Register the type with the " +
			"event type map / source generator (the allow-list) so it can be resolved safely.");
	}

	#endregion IEventSerializer - byte[] and type resolution methods
}
