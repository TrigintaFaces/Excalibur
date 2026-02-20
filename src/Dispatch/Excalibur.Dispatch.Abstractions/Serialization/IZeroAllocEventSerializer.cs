// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Zero-allocation event serializer using Span/Memory for high-performance scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IEventSerializer"/> with Span-based overloads that enable
/// zero-allocation serialization when combined with <see cref="System.Buffers.ArrayPool{T}"/>.
/// </para>
/// <para>
/// <b>Usage Pattern:</b>
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
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
///   <item>Zero allocations when using pooled buffers</item>
///   <item>2-5x faster than JSON-based serializers</item>
///   <item>Designed for high-throughput event store scenarios</item>
/// </list>
/// </remarks>
public interface IZeroAllocEventSerializer : IEventSerializer
{
	/// <summary>
	/// Serializes an event to a caller-provided span buffer.
	/// </summary>
	/// <param name="domainEvent">The event to serialize.</param>
	/// <param name="buffer">
	/// The buffer to write to. Must be large enough to hold the serialized data.
	/// Use <see cref="GetEventSize"/> to determine the required size.
	/// </param>
	/// <returns>The number of bytes written to the buffer.</returns>
	/// <exception cref="ArgumentNullException">Thrown when domainEvent is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the buffer is too small. Use <see cref="GetEventSize"/> first.
	/// </exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresDynamicCode("Serialization of events requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Serialization may reference types not preserved during trimming")]
	int SerializeEvent(IDomainEvent domainEvent, Span<byte> buffer);

	/// <summary>
	/// Deserializes an event from a read-only span (zero-copy).
	/// </summary>
	/// <param name="data">The serialized event data.</param>
	/// <param name="eventType">The type of event to deserialize.</param>
	/// <returns>The deserialized event.</returns>
	/// <exception cref="ArgumentNullException">Thrown when eventType is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresDynamicCode("Deserialization of events requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Deserialization may reference types not preserved during trimming")]
	IDomainEvent DeserializeEvent(ReadOnlySpan<byte> data, Type eventType);

	/// <summary>
	/// Gets the required buffer size for serializing an event.
	/// </summary>
	/// <param name="domainEvent">The event to measure.</param>
	/// <returns>
	/// The minimum buffer size required for serialization. The returned value includes
	/// a safety margin to handle serializer overhead.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when domainEvent is null.</exception>
	/// <remarks>
	/// Use this method to determine the buffer size when renting from
	/// <see cref="System.Buffers.ArrayPool{T}"/>. The returned size is conservative
	/// to ensure the buffer is always large enough.
	/// </remarks>
	[RequiresDynamicCode("Size calculation may require dynamic code generation")]
	[RequiresUnreferencedCode("Size calculation may reference types not preserved during trimming")]
	int GetEventSize(IDomainEvent domainEvent);

	/// <summary>
	/// Serializes a snapshot to a caller-provided span buffer.
	/// </summary>
	/// <param name="snapshot">The snapshot to serialize.</param>
	/// <param name="buffer">
	/// The buffer to write to. Must be large enough to hold the serialized data.
	/// Use <see cref="GetSnapshotSize"/> to determine the required size.
	/// </param>
	/// <returns>The number of bytes written to the buffer.</returns>
	/// <exception cref="ArgumentNullException">Thrown when snapshot is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when the buffer is too small. Use <see cref="GetSnapshotSize"/> first.
	/// </exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	[RequiresDynamicCode("Serialization of snapshots requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Serialization may reference types not preserved during trimming")]
	int SerializeSnapshot(object snapshot, Span<byte> buffer);

	/// <summary>
	/// Deserializes a snapshot from a read-only span (zero-copy).
	/// </summary>
	/// <param name="data">The serialized snapshot data.</param>
	/// <param name="snapshotType">The type of snapshot to deserialize.</param>
	/// <returns>The deserialized snapshot.</returns>
	/// <exception cref="ArgumentNullException">Thrown when snapshotType is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	[RequiresDynamicCode("Deserialization of snapshots requires dynamic code generation for type inspection")]
	[RequiresUnreferencedCode("Deserialization may reference types not preserved during trimming")]
	object DeserializeSnapshot(ReadOnlySpan<byte> data, Type snapshotType);

	/// <summary>
	/// Gets the required buffer size for serializing a snapshot.
	/// </summary>
	/// <param name="snapshot">The snapshot to measure.</param>
	/// <returns>
	/// The minimum buffer size required for serialization. The returned value includes
	/// a safety margin to handle serializer overhead.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when snapshot is null.</exception>
	/// <remarks>
	/// Use this method to determine the buffer size when renting from
	/// <see cref="System.Buffers.ArrayPool{T}"/>. The returned size is conservative
	/// to ensure the buffer is always large enough.
	/// </remarks>
	[RequiresDynamicCode("Size calculation may require dynamic code generation")]
	[RequiresUnreferencedCode("Size calculation may reference types not preserved during trimming")]
	int GetSnapshotSize(object snapshot);
}
