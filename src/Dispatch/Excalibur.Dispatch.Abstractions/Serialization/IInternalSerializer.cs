// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Internal binary serializer for framework wire formats.
/// Not intended for public/user message serialization - use <see cref="IJsonSerializer"/>
/// or <see cref="IMessageSerializer"/> for that purpose.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides high-performance binary serialization for internal framework
/// communication including outbox/inbox persistence, transport envelopes, and event store payloads.
/// </para>
/// <para>
/// The default implementation uses MemoryPack for optimal performance with AOT/trimming support.
/// Custom implementations can be registered via DI for specialized scenarios.
/// </para>
/// <para>
/// See the design documentation for the full design rationale and versioning strategy.
/// </para>
/// </remarks>
public interface IInternalSerializer
{
	/// <summary>
	/// Serializes a value directly to a buffer writer (zero-copy when possible).
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="bufferWriter">The buffer writer to write serialized bytes to.</param>
	/// <remarks>
	/// This overload is preferred for high-performance scenarios where the caller
	/// already has a buffer writer (e.g., from System.IO.Pipelines).
	/// </remarks>
	void Serialize<T>(T value, IBufferWriter<byte> bufferWriter);

	/// <summary>
	/// Serializes a value to a byte array.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>A byte array containing the serialized representation.</returns>
	/// <remarks>
	/// This overload allocates a new byte array. For high-throughput scenarios,
	/// consider using <see cref="Serialize{T}(T, IBufferWriter{byte})"/> instead.
	/// </remarks>
	byte[] Serialize<T>(T value);

	/// <summary>
	/// Deserializes a value from a <see cref="ReadOnlySequence{T}"/> (supports pipelining).
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="buffer">The buffer containing serialized bytes.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="System.Runtime.Serialization.SerializationException">
	/// Thrown when deserialization fails or returns null.
	/// </exception>
	/// <remarks>
	/// This overload supports non-contiguous buffers from System.IO.Pipelines,
	/// enabling efficient streaming scenarios without copying data.
	/// </remarks>
	T Deserialize<T>(ReadOnlySequence<byte> buffer);

	/// <summary>
	/// Deserializes a value from a contiguous <see cref="ReadOnlySpan{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="buffer">The span containing serialized bytes.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="System.Runtime.Serialization.SerializationException">
	/// Thrown when deserialization fails or returns null.
	/// </exception>
	T Deserialize<T>(ReadOnlySpan<byte> buffer);
}
