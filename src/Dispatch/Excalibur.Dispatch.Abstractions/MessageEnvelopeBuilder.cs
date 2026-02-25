// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Builder for constructing message envelopes with fluent API.
/// </summary>
/// <typeparam name="T"> The type of message payload. </typeparam>
public sealed class MessageEnvelopeBuilder<T>
	where T : IDispatchMessage
{
	private T? _message;
	private IMessageContext? _context;
	private string? _messageId;

	/// <summary>
	/// Sets the message payload.
	/// </summary>
	/// <param name="message"> The message payload to include in the envelope. </param>
	public MessageEnvelopeBuilder<T> WithMessage(T message)
	{
		_message = message;
		return this;
	}

	/// <summary>
	/// Sets the message context.
	/// </summary>
	/// <param name="context"> The message context containing metadata. </param>
	public MessageEnvelopeBuilder<T> WithContext(IMessageContext context)
	{
		_context = context;
		return this;
	}

	/// <summary>
	/// Sets the message ID.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	public MessageEnvelopeBuilder<T> WithMessageId(string messageId)
	{
		_messageId = messageId;
		return this;
	}

	/// <summary>
	/// Sets the timestamp.
	/// </summary>
	/// <param name="timestamp"> The timestamp when the message was created or sent. </param>
	public MessageEnvelopeBuilder<T> WithTimestamp(DateTimeOffset timestamp)
	{
		_ = timestamp; // Placeholder implementation
		return this;
	}

	/// <summary>
	/// Sets the raw message body.
	/// </summary>
	/// <param name="rawBody"> The raw byte representation of the message body. </param>
	public MessageEnvelopeBuilder<T> WithRawBody(ReadOnlyMemory<byte> rawBody)
	{
		_ = rawBody; // Placeholder implementation
		return this;
	}

	/// <summary>
	/// Sets the memory owner for pooled scenarios.
	/// </summary>
	/// <param name="memoryOwner"> The memory owner for managing pooled byte arrays. </param>
	public MessageEnvelopeBuilder<T> WithMemoryOwner(IMemoryOwner<byte> memoryOwner)
	{
		_ = memoryOwner; // Placeholder implementation
		return this;
	}

	/// <summary>
	/// Sets the acknowledgment callbacks.
	/// </summary>
	/// <param name="onAcknowledge"> The callback function to invoke when the message is acknowledged. </param>
	/// <param name="onReject"> The callback function to invoke when the message is rejected, with an optional reason. </param>
	public MessageEnvelopeBuilder<T> WithCallbacks(
		Func<CancellationToken, Task> onAcknowledge,
		Func<string?, CancellationToken, Task> onReject)
	{
		_ = onAcknowledge; // Placeholder implementation
		_ = onReject; // Placeholder implementation
		return this;
	}

	/// <summary>
	/// Builds the message envelope.
	/// </summary>
	/// <returns> The constructed message envelope. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when required fields are missing. </exception>
	public MessageEnvelope Build()
	{
		if (_message is null)
		{
			throw new InvalidOperationException(ErrorMessages.MessageIsRequired);
		}

		if (_context is null)
		{
			throw new InvalidOperationException(ErrorMessages.ContextIsRequired);
		}

		// Create the envelope with the message
		return new MessageEnvelope(_message)
		{
			MessageId = _messageId ?? Guid.NewGuid().ToString(),
			CorrelationId = _context.CorrelationId ?? Guid.NewGuid().ToString(),
			CausationId = _context.CausationId,
			ContentType = "application/json",
		};
	}
}
