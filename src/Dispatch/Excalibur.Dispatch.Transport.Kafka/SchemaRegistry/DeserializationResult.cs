// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents the result of deserializing a Confluent wire format message.
/// </summary>
/// <remarks>
/// <para>
/// This type provides runtime type information alongside the deserialized message,
/// enabling dynamic dispatch scenarios where the message type is only known after
/// deserialization.
/// </para>
/// </remarks>
public sealed class DeserializationResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeserializationResult"/> class.
	/// </summary>
	/// <param name="message">The deserialized message.</param>
	/// <param name="messageType">The .NET type of the message.</param>
	/// <param name="schemaId">The Schema Registry ID used for deserialization.</param>
	/// <param name="version">The message version (if applicable).</param>
	public DeserializationResult(object message, Type messageType, int schemaId, int version)
	{
		Message = message ?? throw new ArgumentNullException(nameof(message));
		MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
		SchemaId = schemaId;
		Version = version;
	}

	/// <summary>
	/// Gets the deserialized message.
	/// </summary>
	/// <value>The message object.</value>
	public object Message { get; }

	/// <summary>
	/// Gets the .NET type of the deserialized message.
	/// </summary>
	/// <value>The message type.</value>
	public Type MessageType { get; }

	/// <summary>
	/// Gets the Schema Registry ID that was used for deserialization.
	/// </summary>
	/// <value>The schema ID from the wire format header.</value>
	public int SchemaId { get; }

	/// <summary>
	/// Gets the message version (for versioned messages).
	/// </summary>
	/// <value>
	/// The version number if the message implements <c>IVersionedMessage</c>; otherwise, 1.
	/// </value>
	public int Version { get; }

	/// <summary>
	/// Casts the deserialized message to the specified type.
	/// </summary>
	/// <typeparam name="T">The expected message type.</typeparam>
	/// <returns>The message cast to <typeparamref name="T"/>.</returns>
	/// <exception cref="InvalidCastException">
	/// The message cannot be cast to <typeparamref name="T"/>.
	/// </exception>
	public T As<T>() => (T)Message;
}
