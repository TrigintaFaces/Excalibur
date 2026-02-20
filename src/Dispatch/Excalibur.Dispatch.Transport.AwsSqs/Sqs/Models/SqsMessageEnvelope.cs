// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS-specific message wrapper that adapts SQS messages to the unified envelope pattern.
/// </summary>
/// <remarks>
/// This class provides a thin adapter layer that preserves AWS-specific metadata while delegating to the unified MessageEnvelope for core functionality.
/// </remarks>
public sealed class SqsMessageEnvelope : IDisposable, IAsyncDisposable
{
	private volatile bool _disposed;
	private readonly Func<CancellationToken, Task>? _onAcknowledge;
	private readonly Func<string?, CancellationToken, Task>? _onReject;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqsMessageEnvelope" /> class.
	/// </summary>
	public SqsMessageEnvelope(
		Message? sqsMessage,
		IDispatchMessage? dispatchMessage,
		IMessageContext messageContext,
		int pollerId = 0,
		Func<CancellationToken, Task>? onAcknowledge = null,
		Func<string?, CancellationToken, Task>? onReject = null)
	{
		ArgumentNullException.ThrowIfNull(messageContext);
		_onAcknowledge = onAcknowledge;
		_onReject = onReject;

		SqsMessage = sqsMessage ?? new Message();

		// Add SQS-specific attributes to context
		if (sqsMessage?.Attributes != null)
		{
			foreach (var attr in sqsMessage.Attributes)
			{
				messageContext.Items[$"SQS.{attr.Key}"] = attr.Value;
			}
		}

		// Add SQS metadata
		if (sqsMessage != null)
		{
			messageContext.Items["SQS.ReceiptHandle"] = sqsMessage.ReceiptHandle ?? string.Empty;
			messageContext.Items["SQS.MessageId"] = sqsMessage.MessageId ?? string.Empty;
			messageContext.Items["SQS.MD5OfBody"] = sqsMessage.MD5OfBody ?? string.Empty;
		}

		if (pollerId != 0)
		{
			messageContext.Items["SQS.PollerId"] = pollerId.ToString(CultureInfo.InvariantCulture);
		}

		// Create unified envelope
		var metadata = MessageMetadata.FromContext(messageContext);
		Envelope = new MessageEnvelope<IDispatchMessage>(
			dispatchMessage ?? new EmptyMessage(),
			metadata,
			messageContext);
	}

	/// <summary>
	/// Gets the underlying AWS SQS message.
	/// </summary>
	/// <value>
	/// The underlying AWS SQS message.
	/// </value>
	public Message SqsMessage { get; }

	/// <summary>
	/// Gets the unified message envelope.
	/// </summary>
	/// <value>
	/// The unified message envelope.
	/// </value>
	public MessageEnvelope<IDispatchMessage> Envelope { get; }

	/// <summary>
	/// Gets the dispatch message.
	/// </summary>
	/// <value>
	/// The dispatch message.
	/// </value>
	public IDispatchMessage? Message => Envelope.Message;

	/// <summary>
	/// Gets the message context with all metadata.
	/// </summary>
	/// <value>
	/// The message context with all metadata.
	/// </value>
	public IMessageContext Context => Envelope.Context!;

	/// <summary>
	/// Gets the receipt handle for acknowledgment.
	/// </summary>
	/// <value>
	/// The receipt handle for acknowledgment.
	/// </value>
	public string? ReceiptHandle => SqsMessage?.ReceiptHandle;

	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string? MessageId => SqsMessage?.MessageId;

	/// <summary>
	/// Gets the message attributes from SQS.
	/// </summary>
	/// <value>
	/// The message attributes from SQS.
	/// </value>
	public Dictionary<string, MessageAttributeValue>? MessageAttributes => SqsMessage?.MessageAttributes;

	/// <summary>
	/// Gets or sets the approximate receive count.
	/// </summary>
	/// <value>
	/// The approximate receive count.
	/// </value>
	public int ApproximateReceiveCount
	{
		get
		{
			var attr = Context.Items;
			if (attr != null && attr.TryGetValue("SQS.ApproximateReceiveCount", out var value) &&
				value is string strValue && int.TryParse(strValue, out var count))
			{
				return count;
			}

			return Context.DeliveryCount;
		}
		set => Context.Items["SQS.ApproximateReceiveCount"] = value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Gets the sent timestamp.
	/// </summary>
	/// <value>
	/// The sent timestamp.
	/// </value>
	public DateTime SentTimestamp => Envelope.Timestamp.UtcDateTime;

	/// <summary>
	/// Gets or sets the first receive timestamp.
	/// </summary>
	/// <value>
	/// The first receive timestamp.
	/// </value>
	public DateTime? FirstReceiveTimestamp
	{
		get
		{
			var attr = Context.Items;
			if (attr != null && attr.TryGetValue("SQS.ApproximateFirstReceiveTimestamp", out var value) &&
				value is string strValue && long.TryParse(strValue, out var unixMs))
			{
				return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
			}

			return null;
		}

		set
		{
			if (value.HasValue)
			{
				var unixMs = new DateTimeOffset(value.Value).ToUnixTimeMilliseconds();
				Context.Items["SQS.ApproximateFirstReceiveTimestamp"] = unixMs.ToString(CultureInfo.InvariantCulture);
			}
		}
	}

	/// <summary>
	/// Gets when the message was received.
	/// </summary>
	/// <value>
	/// When the message was received.
	/// </value>
	public DateTime ReceivedAt => Envelope.Timestamp.UtcDateTime;

	/// <summary>
	/// Gets or sets the ID of the long poller.
	/// </summary>
	/// <value>
	/// The ID of the long poller.
	/// </value>
	public int PollerId
	{
		get
		{
			var attr = Context.Items;
			if (attr != null && attr.TryGetValue("SQS.PollerId", out var value) &&
				value is string strValue && int.TryParse(strValue, out var id))
			{
				return id;
			}

			return 0;
		}
		set => Context.Items["SQS.PollerId"] = value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Acknowledges the message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task AcknowledgeAsync(CancellationToken cancellationToken)
	{
		if (_onAcknowledge != null)
		{
			await _onAcknowledge(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Rejects the message.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task RejectAsync(CancellationToken cancellationToken, string? reason = null)
	{
		if (_onReject != null)
		{
			await _onReject(reason, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// MessageEnvelope<T> doesn't implement IDisposable Clean up any local resources if needed
		_disposed = true;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		// MessageEnvelope<T> doesn't implement IAsyncDisposable Clean up any local resources if needed
		_disposed = true;
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Empty message for backwards compatibility.
	/// </summary>
	private sealed class EmptyMessage : IDispatchMessage
	{
		private readonly Dictionary<string, object> _headers = [];
		private readonly DefaultMessageFeatures _features = new();

		public Guid Id => Guid.Empty;

		/// <summary>
		/// Empty messages are treated as documents.
		/// </summary>
		public MessageKinds Kind => MessageKinds.Document;

		public string MessageId => Guid.Empty.ToString();

		public DateTimeOffset Timestamp => DateTimeOffset.MinValue;

		public IReadOnlyDictionary<string, object> Headers => _headers;

		public object Body => string.Empty;

		public string MessageType => "Empty";

		public IMessageFeatures Features => _features;
	}
}
