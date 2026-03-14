// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Event serializer implementation using the pluggable serialization infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This serializer implements <see cref="IEventSerializer"/> by delegating to the
/// configured <see cref="ISerializer"/> (typically MemoryPack for highest performance).
/// Also provides Span-based overloads for zero-allocation scenarios.
/// </para>
/// <para>
/// <b>Usage Pattern (Span-based):</b>
/// </para>
/// <code>
/// var size = serializer.GetEventSize(evt);
/// var buffer = ArrayPool&lt;byte&gt;.Shared.Rent(size);
/// try
/// {
///     var written = serializer.SerializeEvent(evt, buffer);
///     await StoreAsync(buffer.AsSpan(0, written), ct);
/// }
/// finally
/// {
///     ArrayPool&lt;byte&gt;.Shared.Return(buffer);
/// }
/// </code>
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
	/// Thrown when MemoryPack serializer is not registered.
	/// </exception>
	public SpanEventSerializer(ISerializerRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		// Prefer MemoryPack for best Span support, fall back to current serializer
		_pluggable = registry.GetByName("MemoryPack")
					 ?? registry.GetById(SerializerIds.MemoryPack)
					 ?? registry.GetCurrent().Serializer
					 ?? throw new InvalidOperationException(
						"No serializer available. Register MemoryPack or configure a default serializer.");
	}

	#region Span-based methods

	/// <summary>
	/// Serializes an event to a caller-provided span buffer.
	/// </summary>
	[RequiresDynamicCode("Serialization of events requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Serialization may reference types not preserved during trimming")]
	public int SerializeEvent(IDomainEvent domainEvent, Span<byte> buffer)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var eventType = domainEvent.GetType();
		var bytes = _pluggable.SerializeObject(domainEvent, eventType);

		if (bytes.Length > buffer.Length)
		{
			throw new ArgumentException(
				$"Buffer too small. Required: {bytes.Length}, Available: {buffer.Length}. " +
				$"Use GetEventSize() to determine required buffer size.",
				nameof(buffer));
		}

		bytes.CopyTo(buffer);
		return bytes.Length;
	}

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
	/// Gets the required buffer size for serializing an event.
	/// </summary>
	[RequiresDynamicCode("Size calculation may require dynamic code generation")]
	[RequiresUnreferencedCode("Size calculation may reference types not preserved during trimming")]
	public int GetEventSize(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		// Serialize once to get exact size, then add margin
		var eventType = domainEvent.GetType();
		var bytes = _pluggable.SerializeObject(domainEvent, eventType);
		return bytes.Length + SizeMargin;
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
	[RequiresDynamicCode("JSON serialization of events requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	public byte[] SerializeEvent(IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var eventType = domainEvent.GetType();
		return _pluggable.SerializeObject(domainEvent, eventType);
	}

	/// <inheritdoc />
	[RequiresDynamicCode("JSON deserialization of events requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON deserialization may reference types not preserved during trimming")]
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

		return TypeResolution.TypeResolver.ResolveType(typeName)
			   ?? throw new SerializationException($"Cannot resolve type: {typeName}");
	}

	#endregion IEventSerializer - byte[] and type resolution methods
}
