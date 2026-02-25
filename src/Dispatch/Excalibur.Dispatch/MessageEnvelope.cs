// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Generic message envelope for strongly-typed message handling.
/// </summary>
/// <typeparam name="TMessage"> The type of the message. </typeparam>
public sealed class MessageEnvelope<TMessage> : IDisposable
	where TMessage : IDispatchMessage
{
	private readonly MessageEnvelope _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEnvelope{TMessage}"/> class.
	/// </summary>
	public MessageEnvelope(TMessage message, in MessageMetadata metadata)
	{
		Message = message ?? throw new ArgumentNullException(nameof(message));
		Metadata = metadata;
		_inner = new MessageEnvelope(message)
		{
			MessageId = metadata.MessageId,
			CorrelationId = metadata.CorrelationId,
			MessageType = typeof(TMessage).FullName,
			ReceivedTimestampUtc = DateTimeOffset.UtcNow, // Record-based metadata doesn't have TimestampTicks
			DeliveryCount = 0, // Record-based metadata doesn't have DeliveryCount, use default
		};
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEnvelope{TMessage}"/> class.
	/// Initializes a new instance with metadata and context.
	/// </summary>
	public MessageEnvelope(TMessage message, in MessageMetadata metadata, IMessageContext? context)
		: this(message, metadata) =>
		Context = context;

	/// <summary>
	/// Gets the strongly-typed message.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public TMessage Message { get; }

	/// <summary>
	/// Gets the message metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public MessageMetadata Metadata { get; }

	/// <summary>
	/// Gets or sets the message context.
	/// </summary>
	/// <value>The current <see cref="Context"/> value.</value>
	public IMessageContext? Context { get; set; }

	/// <summary>
	/// Gets or sets the acknowledge callback.
	/// </summary>
	/// <value>The current <see cref="AcknowledgeMessageAsync"/> value.</value>
	public Func<Task>? AcknowledgeMessageAsync { get; set; }

	/// <summary>
	/// Gets or sets the reject callback.
	/// </summary>
	/// <value>The current <see cref="RejectMessageAsync"/> value.</value>
	public Func<string?, Task>? RejectMessageAsync { get; set; }

	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string MessageId => _inner.MessageId ?? string.Empty;

	/// <summary>
	/// Gets the correlation ID.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId => _inner.CorrelationId;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value>The current <see cref="Headers"/> value.</value>
	public IDictionary<string, string> Headers => _inner.Headers;

	/// <summary>
	/// Gets the timestamp.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp => _inner.ReceivedTimestampUtc;

	/// <summary>
	/// Gets the retry count.
	/// </summary>
	/// <value>The current <see cref="RetryCount"/> value.</value>
	public int RetryCount => _inner.DeliveryCount;

	/// <summary>
	/// Creates a new envelope with updated metadata.
	/// </summary>
	public MessageEnvelope<TMessage> WithMetadata(in MessageMetadata metadata) =>
		new(Message, metadata, Context) { AcknowledgeMessageAsync = AcknowledgeMessageAsync, RejectMessageAsync = RejectMessageAsync };

	/// <summary>
	/// Releases resources held by the inner envelope.
	/// </summary>
	public void Dispose() => _inner.Dispose();
}
