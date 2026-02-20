// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Factory for creating and parsing message envelopes for Azure Storage Queue messages.
/// </summary>
public interface IMessageEnvelopeFactory
{
	/// <summary>
	/// Creates a message envelope for sending to the queue.
	/// </summary>
	/// <param name="message"> The message to envelope. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The serialized message envelope. </returns>
	string CreateEnvelope(object message, IMessageContext context);

	/// <summary>
	/// Parses a queue message to extract the original message and context.
	/// </summary>
	/// <param name="queueMessage"> The queue message to parse. </param>
	/// <returns> The parsed message result containing the original message and context. </returns>
	[RequiresUnreferencedCode("Message envelope deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Message envelope deserialization uses reflection to dynamically create and populate types")]
	ParsedMessageResult ParseMessage(QueueMessage queueMessage);

	/// <summary>
	/// Attempts to parse a queue message as a specific message type.
	/// </summary>
	/// <typeparam name="T"> The type of message to parse. </typeparam>
	/// <param name="queueMessage"> The queue message to parse. </param>
	/// <param name="parsedMessage"> The parsed message if successful. </param>
	/// <param name="context"> The parsed message context if successful. </param>
	/// <returns> True if parsing succeeded; otherwise, false. </returns>
	bool TryParseMessage<T>(QueueMessage queueMessage, out T? parsedMessage, out IMessageContext? context);

	/// <summary>
	/// Creates a message context from queue message metadata.
	/// </summary>
	/// <param name="queueMessage"> The queue message. </param>
	/// <returns> The message context with populated metadata. </returns>
	IMessageContext CreateContext(QueueMessage queueMessage);

	/// <summary>
	/// Validates that a queue message envelope is well-formed.
	/// </summary>
	/// <param name="queueMessage"> The queue message to validate. </param>
	/// <returns> True if the envelope is valid; otherwise, false. </returns>
	bool IsValidEnvelope(QueueMessage queueMessage);
}
