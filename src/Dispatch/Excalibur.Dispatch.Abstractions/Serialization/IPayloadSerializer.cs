// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// High-level abstraction for serializing and deserializing payloads with automatic format detection.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary interface used by Outbox, Inbox, and Event Store for payload serialization.
/// It provides a facade over <see cref="ISerializerRegistry"/> and handles:
/// </para>
/// <list type="bullet">
///   <item>Magic byte prepending during serialization</item>
///   <item>Automatic format detection during deserialization</item>
///   <item>Routing to the correct <see cref="IPluggableSerializer"/> implementation</item>
/// </list>
/// <para>
/// <b>Payload Format:</b>
/// </para>
/// <code>
/// [Byte 0: Serializer ID] [Bytes 1..N: Serialized Payload]
/// </code>
/// <para>
/// <b>Performance Characteristics:</b>
/// </para>
/// <list type="bullet">
///   <item>Serialize: ~2ns overhead (magic byte prepend)</item>
///   <item>Deserialize (fast path): &lt;1ns overhead (current serializer match)</item>
///   <item>Deserialize (migration path): ~5ns overhead (registry lookup for legacy format)</item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// // Serialization (prepends magic byte automatically)
/// byte[] payload = payloadSerializer.Serialize(myEvent);
/// // payload[0] contains the serializer ID
///
/// // Deserialization (detects format from magic byte)
/// var myEvent = payloadSerializer.Deserialize&lt;MyEvent&gt;(payload);
/// </code>
/// <para>
/// See the pluggable serialization architecture documentation for the magic byte format.
/// </para>
/// </remarks>
public interface IPayloadSerializer
{
	/// <summary>
	/// Serializes an object to a byte array with magic byte header.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>
	/// Byte array in format: [magic byte (1 byte)][serialized payload (N bytes)].
	/// The magic byte identifies the serializer used.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no current serializer is configured in the registry.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Uses the current serializer from <see cref="ISerializerRegistry.GetCurrent"/> and
	/// prepends its ID as the first byte of the result.
	/// </para>
	/// <para>
	/// The resulting byte array is suitable for direct storage in databases (e.g., VARBINARY columns).
	/// </para>
	/// </remarks>
	byte[] Serialize<T>(T value);

	/// <summary>
	/// Deserializes a byte array to an object, automatically detecting the format.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="data">
	/// Byte array in format: [magic byte (1 byte)][serialized payload (N bytes)].
	/// </param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
	/// <exception cref="SerializationException">
	/// Thrown when:
	/// <list type="bullet">
	///   <item>The payload is empty</item>
	///   <item>The magic byte identifies an unknown/unregistered serializer</item>
	///   <item>Deserialization fails</item>
	/// </list>
	/// </exception>
	/// <remarks>
	/// <para>
	/// The method reads the magic byte (first byte) to determine which serializer to use:
	/// </para>
	/// <list type="bullet">
	///   <item><b>Fast path:</b> If magic byte matches current serializer ID, uses current serializer directly</item>
	///   <item><b>Migration path:</b> If magic byte differs, looks up serializer in registry by ID</item>
	/// </list>
	/// <para>
	/// This design enables seamless migration between serializers - old data remains readable
	/// as long as the original serializer is still registered.
	/// </para>
	/// </remarks>
	T Deserialize<T>(byte[] data);

	/// <summary>
	/// Gets the serializer ID that would be used for the next serialization operation.
	/// </summary>
	/// <returns>The ID of the current serializer.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no current serializer is configured.
	/// </exception>
	/// <remarks>
	/// Useful for diagnostics and testing to verify which serializer is active.
	/// </remarks>
	byte GetCurrentSerializerId();

	/// <summary>
	/// Gets the name of the serializer that would be used for the next serialization operation.
	/// </summary>
	/// <returns>The name of the current serializer.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no current serializer is configured.
	/// </exception>
	/// <remarks>
	/// Useful for diagnostics and logging.
	/// </remarks>
	string GetCurrentSerializerName();

	/// <summary>
	/// Serializes an object to a byte array with magic byte header, using the specified runtime type.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <param name="type">The runtime type to use for serialization.</param>
	/// <returns>
	/// Byte array in format: [magic byte (1 byte)][serialized payload (N bytes)].
	/// The magic byte identifies the serializer used.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when value or type is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no current serializer is configured in the registry.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload is useful when the compile-time type differs from the runtime type,
	/// such as when serializing interface references (e.g., IDispatchMessage) that hold
	/// concrete types at runtime. Binary serializers like MemoryPack and MessagePack
	/// require concrete types for proper serialization.
	/// </para>
	/// </remarks>
	byte[] SerializeObject(object value, Type type);

	/// <summary>
	/// Deserializes transport message data with hybrid format detection for external system interoperability.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="data">The raw message payload bytes from a transport (Kafka, RabbitMQ, etc.).</param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
	/// <exception cref="SerializationException">
	/// Thrown when:
	/// <list type="bullet">
	///   <item>The payload is empty</item>
	///   <item>The format cannot be detected (unknown first byte)</item>
	///   <item>Confluent Schema Registry format with non-JSON payload (Avro/Protobuf not supported)</item>
	///   <item>Deserialization fails</item>
	/// </list>
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method implements hybrid format detection for transport messages that may originate
	/// from both internal and external systems. The detection order is:
	/// </para>
	/// <list type="number">
	///   <item><b>Our magic bytes (1-254):</b> Use registered IPluggableSerializer via <see cref="Deserialize{T}"/></item>
	///   <item><b>Confluent Schema Registry format (0x00 + 4 bytes):</b> Skip 5-byte header, attempt JSON on payload</item>
	///   <item><b>Raw JSON (0x7B or 0x5B):</b> Direct JSON deserialization for plain JSON from external systems</item>
	///   <item><b>Unknown:</b> Throw <see cref="SerializationException"/> with diagnostic information</item>
	/// </list>
	/// <para>
	/// <b>Confluent Schema Registry Format:</b>
	/// </para>
	/// <code>
	/// [Byte 0: 0x00] [Bytes 1-4: Schema ID (big endian)] [Bytes 5+: Serialized Payload]
	/// </code>
	/// <para>
	/// This method supports Confluent format for <b>inbound messages only</b> (consuming from Kafka producers
	/// using Schema Registry). Outbound Confluent format production requires the full Schema Registry integration
	/// (deferred to future epic).
	/// </para>
	/// <para>
	/// See the architecture documentation for details.
	/// </para>
	/// </remarks>
	T DeserializeTransportMessage<T>(byte[] data);
}
