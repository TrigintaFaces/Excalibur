// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Extension methods for creating struct-based message envelopes.
/// </summary>
public static class DeliveryMessageEnvelopeExtensions
{
	/// <summary>
	/// Creates a message envelope from a message with default metadata.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message. </typeparam>
	/// <param name="message"> The message to wrap. </param>
	/// <returns> A message envelope. </returns>
	public static MessageEnvelope<TMessage> ToEnvelope<TMessage>(this TMessage message)
		where TMessage : IDispatchMessage =>
		new(message, MessageMetadata.Default.ToRecordMetadata());

	/// <summary>
	/// Creates a message envelope from a message with custom metadata.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message. </typeparam>
	/// <param name="message"> The message to wrap. </param>
	/// <param name="configureMetadata"> Action to configure metadata. </param>
	/// <returns> A message envelope. </returns>
	public static MessageEnvelope<TMessage> ToEnvelope<TMessage>(
		this TMessage message,
		Func<MessageMetadataBuilder, Messaging.MessageMetadata> configureMetadata)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(configureMetadata);

		var builder = new MessageMetadataBuilder();
		var metadata = configureMetadata(builder);
		return new MessageEnvelope<TMessage>(message, metadata);
	}
}
