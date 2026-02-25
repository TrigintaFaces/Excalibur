// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Deserializes messages from Confluent wire format (magic byte + schema ID + payload).
/// </summary>
/// <remarks>
/// <para>
/// Implementations consume byte arrays in the Confluent wire format:
/// </para>
/// <list type="bullet">
///   <item><description>Byte 0: Magic byte (0x00)</description></item>
///   <item><description>Bytes 1-4: Schema ID (big-endian 32-bit integer)</description></item>
///   <item><description>Bytes 5+: Serialized message payload</description></item>
/// </list>
/// <para>
/// The schema ID is used to resolve the .NET type via <see cref="ISchemaTypeResolver"/>,
/// enabling runtime type determination without knowing the message type at compile time.
/// </para>
/// </remarks>
public interface IConfluentFormatDeserializer
{
	/// <summary>
	/// Deserializes a message from Confluent wire format to a known type.
	/// </summary>
	/// <typeparam name="T">The expected message type.</typeparam>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="data">The wire format message bytes.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The deserialized message.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Schema lookup failed or deserialization encountered an error.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The wire format is invalid (wrong magic byte or insufficient length).
	/// </exception>
	Task<T> DeserializeAsync<T>(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deserializes a message from Confluent wire format with runtime type resolution.
	/// </summary>
	/// <param name="topic">The Kafka topic name (used for subject naming).</param>
	/// <param name="data">The wire format message bytes.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A result containing the deserialized message and its type information.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Schema lookup failed or type resolution encountered an error.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// The wire format is invalid (wrong magic byte or insufficient length).
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload is useful for dynamic dispatch scenarios where the message type
	/// is only known after examining the schema ID in the wire format header.
	/// </para>
	/// <para>
	/// The <see cref="DeserializationResult.MessageType"/> is resolved via
	/// <see cref="ISchemaTypeResolver"/> using the JSON Schema <c>title</c> property.
	/// </para>
	/// </remarks>
	Task<DeserializationResult> DeserializeAsync(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken);
}
