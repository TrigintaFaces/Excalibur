// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch;

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
	private DateTimeOffset? _timestamp;
	private Func<CancellationToken, Task>? _onAcknowledge;
	private Func<string?, CancellationToken, Task>? _onReject;

	/// <summary>
	/// Sets the message payload.
	/// </summary>
	/// <param name="message"> The message payload to include in the envelope. </param>
	public MessageEnvelopeBuilder<T> WithMessage(T message)
	{
		ArgumentNullException.ThrowIfNull(message);
		_message = message;
		return this;
	}

	/// <summary>
	/// Sets the message context.
	/// </summary>
	/// <param name="context"> The message context containing metadata. </param>
	public MessageEnvelopeBuilder<T> WithContext(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		_context = context;
		return this;
	}

	/// <summary>
	/// Sets the message ID.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	public MessageEnvelopeBuilder<T> WithMessageId(string messageId)
	{
		ArgumentException.ThrowIfNullOrEmpty(messageId);
		_messageId = messageId;
		return this;
	}

	/// <summary>
	/// Sets the timestamp.
	/// </summary>
	/// <param name="timestamp"> The timestamp when the message was created or sent. </param>
	public MessageEnvelopeBuilder<T> WithTimestamp(DateTimeOffset timestamp)
	{
		_timestamp = timestamp;
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
		ArgumentNullException.ThrowIfNull(onAcknowledge);
		ArgumentNullException.ThrowIfNull(onReject);
		_onAcknowledge = onAcknowledge;
		_onReject = onReject;
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

		// Create the envelope with the message, then apply the optional builder-supplied slots. The
		// acknowledge/reject callbacks share the envelope's CancellationToken-bearing signature, so they
		// are wired directly (the token flows through to the transport I/O).
		var envelope = new MessageEnvelope(_message)
		{
			MessageId = _messageId ?? Uuid7Extensions.GenerateString(),
			CorrelationId = _context.CorrelationId ?? Uuid7Extensions.GenerateString(),
			CausationId = _context.CausationId,
			ContentType = "application/json",
			AcknowledgeAsync = _onAcknowledge,
			RejectAsync = _onReject,
		};

		if (_timestamp is { } timestamp)
		{
			envelope.Timestamp = timestamp;
		}

		return envelope;
	}
}
