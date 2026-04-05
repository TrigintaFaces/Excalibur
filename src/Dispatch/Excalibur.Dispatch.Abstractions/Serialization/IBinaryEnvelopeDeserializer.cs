// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Provides binary envelope deserialization for inbox and outbox messages
/// when an optimized binary serializer (e.g., MemoryPack) is registered.
/// </summary>
/// <remarks>
/// <para>
/// This interface decouples the outbox/inbox processors from specific binary
/// serializer implementations. When consumers opt into a binary serializer,
/// the serializer package registers an implementation of this interface
/// that handles envelope deserialization using its native format.
/// </para>
/// <para>
/// When no binary serializer is registered, this interface is not resolved
/// from DI and the processors fall back to JSON envelope handling.
/// </para>
/// </remarks>
public interface IBinaryEnvelopeDeserializer
{
	/// <summary>
	/// Deserializes a binary inbox envelope into a serializer-agnostic result.
	/// </summary>
	/// <param name="data"> The binary envelope data (without format marker byte). </param>
	/// <returns> The deserialized envelope data, or null if deserialization fails. </returns>
	EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data);

	/// <summary>
	/// Deserializes a binary outbox envelope into a serializer-agnostic result.
	/// </summary>
	/// <param name="data"> The binary envelope data (without format marker byte). </param>
	/// <returns> The deserialized envelope data, or null if deserialization fails. </returns>
	EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data);
}

/// <summary>
/// Serializer-agnostic result of deserializing a binary inbox or outbox envelope.
/// </summary>
/// <remarks>
/// This type is returned by <see cref="IBinaryEnvelopeDeserializer"/> and contains
/// all the data needed to reconstruct an inbox or outbox message without
/// depending on any specific serializer's envelope types.
/// </remarks>
public sealed class EnvelopeData
{
	/// <summary>
	/// Gets the unique message identifier.
	/// </summary>
	public required Guid MessageId { get; init; }

	/// <summary>
	/// Gets the fully qualified message type name.
	/// </summary>
	public string? MessageType { get; init; }

	/// <summary>
	/// Gets the serialized message payload.
	/// </summary>
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets the timestamp associated with the envelope (received or created).
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets optional metadata or headers associated with the message.
	/// </summary>
	public Dictionary<string, string>? Metadata { get; init; }
}
