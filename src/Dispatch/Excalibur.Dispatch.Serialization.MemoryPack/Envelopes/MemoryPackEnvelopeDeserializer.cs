// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// MemoryPack implementation of <see cref="IBinaryEnvelopeDeserializer"/> that uses
/// <see cref="InboxEnvelope"/> and <see cref="OutboxEnvelope"/> for binary envelope deserialization.
/// </summary>
/// <remarks>
/// This is registered automatically when consumers opt into MemoryPack via
/// <c>AddMemoryPackSerializer()</c>. It enables the inbox/outbox processors to
/// deserialize binary envelope payloads without directly depending on the MemoryPack package.
/// </remarks>
internal sealed class MemoryPackEnvelopeDeserializer : IBinaryEnvelopeDeserializer
{
	private readonly ISerializer _serializer;

	public MemoryPackEnvelopeDeserializer(ISerializer serializer)
	{
		_serializer = serializer;
	}

	/// <inheritdoc />
	public EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data)
	{
		var envelope = _serializer.Deserialize<InboxEnvelope>(data);

		if (envelope is null)
		{
			return null;
		}

		return new EnvelopeData
		{
			MessageId = envelope.MessageId,
			MessageType = envelope.MessageType,
			Payload = envelope.Payload,
			Timestamp = envelope.ReceivedAt,
			Metadata = envelope.Metadata,
		};
	}

	/// <inheritdoc />
	public EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data)
	{
		var envelope = _serializer.Deserialize<OutboxEnvelope>(data);

		if (envelope is null)
		{
			return null;
		}

		return new EnvelopeData
		{
			MessageId = envelope.MessageId,
			MessageType = envelope.MessageType,
			Payload = envelope.Payload,
			Timestamp = envelope.CreatedAt,
			Metadata = envelope.Headers,
		};
	}
}
