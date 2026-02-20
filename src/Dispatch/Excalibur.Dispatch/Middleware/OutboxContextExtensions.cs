// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Extension methods for working with the outbox in message context.
/// </summary>
public static class OutboxContextExtensions
{
	/// <summary>
	/// Adds an outbound message to be staged in the outbox after successful processing.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="message"> The outbound message to stage. </param>
	public static void AddOutboundMessage(this IMessageContext context, OutboundMessage message)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(message);

		var outboundMessages = context.GetItem<List<OutboundMessage>>("OutboundMessages") ?? [];
		outboundMessages.Add(message);
		context.SetItem("OutboundMessages", outboundMessages);
	}

	/// <summary>
	/// Creates and adds an outbound message to be staged in the outbox.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="messageType"> The fully qualified type name of the message. </param>
	/// <param name="payload"> The serialized message payload. </param>
	/// <param name="destination"> The destination where the message should be delivered. </param>
	/// <param name="headers"> Optional message headers and metadata. </param>
	/// <param name="scheduledAt"> Optional scheduled delivery time. </param>
	/// <param name="priority"> Optional message priority (higher values = higher priority). </param>
	public static void AddOutboundMessage(
		this IMessageContext context,
		string messageType,
		byte[] payload,
		string destination,
		Dictionary<string, object>? headers = null,
		DateTimeOffset? scheduledAt = null,
		int priority = 0)
	{
		var message = new OutboundMessage(messageType, payload, destination, headers) { ScheduledAt = scheduledAt, Priority = priority };

		context.AddOutboundMessage(message);
	}

	/// <summary>
	/// Creates and adds an outbound message from a dispatch message object.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="message"> The message to send. </param>
	/// <param name="destination"> The destination where the message should be delivered. </param>
	/// <param name="headers"> Optional message headers and metadata. </param>
	/// <param name="scheduledAt"> Optional scheduled delivery time. </param>
	/// <param name="priority"> Optional message priority (higher values = higher priority). </param>
	[RequiresUnreferencedCode(
		"Outbound message creation uses AssemblyQualifiedName for type resolution which may reference types not preserved during trimming.")]
	[RequiresDynamicCode("Message serialization to JSON requires runtime code generation for type-specific serialization logic.")]
	public static void AddOutboundMessage<TMessage>(
		this IMessageContext context,
		TMessage message,
		string destination,
		Dictionary<string, object>? headers = null,
		DateTimeOffset? scheduledAt = null,
		int priority = 0)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = typeof(TMessage).AssemblyQualifiedName ?? typeof(TMessage).FullName ?? typeof(TMessage).Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message);

		context.AddOutboundMessage(messageType, payload, destination, headers, scheduledAt, priority);
	}

	/// <summary>
	/// Gets the count of outbound messages queued in the context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The number of outbound messages queued. </returns>
	public static int GetOutboundMessageCount(this IMessageContext context)
	{
		var outboundMessages = context.GetItem<List<OutboundMessage>>("OutboundMessages");
		return outboundMessages?.Count ?? 0;
	}

	/// <summary>
	/// Clears all outbound messages from the context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	public static void ClearOutboundMessages(this IMessageContext context) =>
		context.SetItem<List<OutboundMessage>?>("OutboundMessages", value: null);
}
