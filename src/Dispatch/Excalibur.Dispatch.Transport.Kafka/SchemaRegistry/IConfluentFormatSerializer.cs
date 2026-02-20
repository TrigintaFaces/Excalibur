// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Serializes messages to Confluent wire format (magic byte + schema ID + payload).
/// </summary>
/// <remarks>
/// <para>
/// Implementations produce byte arrays in the Confluent wire format:
/// </para>
/// <list type="bullet">
///   <item><description>Byte 0: Magic byte (0x00)</description></item>
///   <item><description>Bytes 1-4: Schema ID (big-endian 32-bit integer)</description></item>
///   <item><description>Bytes 5+: Serialized message payload</description></item>
/// </list>
/// <para>
/// The schema is automatically registered with the Schema Registry if not already present.
/// </para>
/// <para>
/// <strong>Zero-Copy Support:</strong> The <c>SerializeToBufferAsync</c> methods
/// provide zero-copy serialization by writing directly to an <see cref="IBufferWriter{T}"/>,
/// eliminating intermediate <c>byte[]</c> allocations in the hot path.
/// </para>
/// </remarks>
public interface IConfluentFormatSerializer
{
	/// <summary>
	/// Serializes a message to Confluent wire format.
	/// </summary>
	/// <typeparam name="T">The type of message to serialize.</typeparam>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The serialized message in Confluent wire format.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Schema registration failed or the schema is incompatible with existing versions.
	/// </exception>
	Task<byte[]> SerializeAsync<T>(
		string topic,
		T message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Serializes a message to Confluent wire format using a runtime type.
	/// </summary>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="messageType">The runtime type of the message.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The serialized message in Confluent wire format.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Schema registration failed or the schema is incompatible with existing versions.
	/// </exception>
	/// <remarks>
	/// This overload is useful when the message type is only known at runtime,
	/// such as when dispatching <c>IDispatchAction</c> instances.
	/// </remarks>
	Task<byte[]> SerializeAsync(
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Serializes a message to Confluent wire format directly into a buffer writer (zero-copy).
	/// </summary>
	/// <typeparam name="T">The type of message to serialize.</typeparam>
	/// <param name="writer">The buffer writer to write the serialized data to.</param>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of bytes written to the buffer.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="writer"/> or <paramref name="message"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topic"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="SchemaRegistryException">
	/// Schema registration failed or the schema is incompatible with existing versions.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method provides zero-copy serialization by writing the 5-byte Confluent header
	/// and JSON payload directly to the provided buffer, eliminating intermediate allocations.
	/// </para>
	/// <para>
	/// The caller is responsible for providing a properly sized buffer writer
	/// (e.g., <c>PooledBufferWriter</c> or <see cref="System.IO.Pipelines.PipeWriter"/>).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// using var bufferWriter = new PooledBufferWriter();
	/// var bytesWritten = await serializer.SerializeToBufferAsync(bufferWriter, "orders-topic", order, ct);
	/// var data = bufferWriter.WrittenMemory;
	/// </code>
	/// </example>
	ValueTask<int> SerializeToBufferAsync<T>(
		IBufferWriter<byte> writer,
		string topic,
		T message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Serializes a message to Confluent wire format directly into a buffer writer using a runtime type (zero-copy).
	/// </summary>
	/// <param name="writer">The buffer writer to write the serialized data to.</param>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="messageType">The runtime type of the message.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of bytes written to the buffer.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="writer"/>, <paramref name="message"/>, or <paramref name="messageType"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topic"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="SchemaRegistryException">
	/// Schema registration failed or the schema is incompatible with existing versions.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload is useful when the message type is only known at runtime,
	/// such as when dispatching <c>IDispatchAction</c> instances.
	/// </para>
	/// <para>
	/// This method provides zero-copy serialization by writing the 5-byte Confluent header
	/// and JSON payload directly to the provided buffer, eliminating intermediate allocations.
	/// </para>
	/// </remarks>
	ValueTask<int> SerializeToBufferAsync(
		IBufferWriter<byte> writer,
		string topic,
		object message,
		Type messageType,
		CancellationToken cancellationToken);
}
