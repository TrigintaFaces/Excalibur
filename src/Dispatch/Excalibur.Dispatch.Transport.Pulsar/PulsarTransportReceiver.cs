// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Pulsar.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Receives messages from an Apache Pulsar subscription via a DotPulsar consumer, exposing them as
/// <see cref="TransportReceivedMessage"/> with explicit acknowledgement.
/// </summary>
internal sealed partial class PulsarTransportReceiver : ITransportReceiver
{
	private const int MaxUnsettledMessages = 10_000;
	private static readonly TimeSpan DrainTimeout = TimeSpan.FromMilliseconds(50);

	private readonly IConsumer<byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private readonly int _maxBatchSize;
	private readonly ConcurrentDictionary<string, MessageId> _unsettled = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PulsarTransportReceiver"/> class.
	/// </summary>
	/// <param name="consumer">The DotPulsar consumer bound to the subscription.</param>
	/// <param name="source">The source identity (subscription name).</param>
	/// <param name="logger">The logger.</param>
	/// <param name="maxPayloadBytes">The maximum accepted payload size, or <see langword="null"/> for no limit.</param>
	/// <param name="maxBatchSize">The maximum number of messages drained per receive call (must be at least 1).</param>
	public PulsarTransportReceiver(
		IConsumer<byte[]> consumer,
		string source,
		ILogger<PulsarTransportReceiver> logger,
		int? maxPayloadBytes = PayloadSizeGuard.DefaultMaxPayloadBytes,
		int maxBatchSize = 10)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
		_maxBatchSize = maxBatchSize < 1 ? 1 : maxBatchSize;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		if (maxMessages <= 0)
		{
			return [];
		}

		// Cap the requested batch at the configured MaxBatchSize so a single receive never drains more
		// than the tuning option allows, regardless of what the caller requests.
		var limit = Math.Min(maxMessages, _maxBatchSize);

		var messages = new List<TransportReceivedMessage>();
		try
		{
			// First message: block on the caller's token. Subsequent messages: bounded drain with a
			// short timeout so the batch returns promptly once the backlog is exhausted, rather than
			// blocking for the full batch size.
			for (var i = 0; i < limit; i++)
			{
				IMessage<byte[]> pulsarMessage;
				if (i == 0)
				{
					pulsarMessage = await _consumer.Receive(cancellationToken).ConfigureAwait(false);
				}
				else
				{
					using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					drainCts.CancelAfter(DrainTimeout);
					try
					{
						pulsarMessage = await _consumer.Receive(drainCts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
					{
						break; // drain timeout — no more messages ready
					}
				}

				var receiptHandle = pulsarMessage.MessageId.ToString();
				try
				{
					PayloadSizeGuard.EnsureWithinLimit(pulsarMessage.Value()?.Length ?? 0, _maxPayloadBytes);
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard: acknowledge (skip) the oversized message so the subscription
					// does not redeliver it forever, then continue the batch.
					LogPayloadTooLargeRejected(Source, pulsarMessage.Value()?.Length ?? 0, ex);
					await _consumer.Acknowledge(pulsarMessage.MessageId, cancellationToken).ConfigureAwait(false);
					continue;
				}

				_unsettled[receiptHandle] = pulsarMessage.MessageId;
				if (_unsettled.Count > MaxUnsettledMessages)
				{
					LogUnsettledOverflow(Source, _unsettled.Count);
				}

				messages.Add(ConvertToReceivedMessage(pulsarMessage, receiptHandle));
				LogMessageReceived(receiptHandle, Source);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Caller cancelled — return whatever was drained so far.
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			throw;
		}

		return messages;
	}

	/// <inheritdoc />
	public async Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		var receiptHandle = GetReceiptHandle(message);
		if (!_unsettled.TryGetValue(receiptHandle, out var messageId))
		{
			throw new InvalidOperationException(
				$"Message with receipt handle '{receiptHandle}' not found in the unsettled cache. It may have already been settled.");
		}

		try
		{
			await _consumer.Acknowledge(messageId, cancellationToken).ConfigureAwait(false);
			_ = _unsettled.TryRemove(receiptHandle, out _);
			LogMessageAcknowledged(receiptHandle, Source);
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(receiptHandle, Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		var receiptHandle = GetReceiptHandle(message);
		if (!_unsettled.TryGetValue(receiptHandle, out var messageId))
		{
			return;
		}

		if (requeue)
		{
			// Negative-acknowledge so Pulsar redelivers after the negative-ack delay.
			await _consumer.RedeliverUnacknowledgedMessages([messageId], cancellationToken).ConfigureAwait(false);
			_ = _unsettled.TryRemove(receiptHandle, out _);
			LogMessageRejectedRequeue(receiptHandle, Source, reason ?? "no reason");
		}
		else
		{
			// Acknowledge to skip the message (DLQ routing handled by the decorator layer).
			await _consumer.Acknowledge(messageId, cancellationToken).ConfigureAwait(false);
			_ = _unsettled.TryRemove(receiptHandle, out _);
			LogMessageRejected(receiptHandle, Source, reason ?? "no reason");
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType == typeof(IConsumer<byte[]>) ? _consumer : null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		await _consumer.DisposeAsync().ConfigureAwait(false);
		LogDisposed(Source);
		GC.SuppressFinalize(this);
	}

	private TransportReceivedMessage ConvertToReceivedMessage(IMessage<byte[]> pulsarMessage, string receiptHandle)
	{
		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var (key, value) in pulsarMessage.Properties)
		{
			properties[key] = value;
		}

		var contentType = properties.TryGetValue("content-type", out var ct) ? ct as string : null;
		var messageType = properties.TryGetValue("message-type", out var mt) ? mt as string : null;
		var correlationId = properties.TryGetValue(OutboxHeaderNames.CorrelationId, out var ci) ? ci as string : null;
		var messageId = properties.TryGetValue("message-id", out var mi) ? mi as string : null;

		return new TransportReceivedMessage
		{
			Id = messageId ?? receiptHandle,
			Body = pulsarMessage.Value() ?? [],
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			Source = Source,
			PartitionKey = pulsarMessage.HasKey ? pulsarMessage.Key : null,
			DeliveryCount = (int)pulsarMessage.RedeliveryCount,
			EnqueuedAt = pulsarMessage.PublishTimeAsDateTimeOffset,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["pulsar.message_id"] = receiptHandle,
				["pulsar.receipt_handle"] = receiptHandle,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("pulsar.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}
		throw new InvalidOperationException("Message does not contain a Pulsar receipt handle in ProviderData.");
	}

	[LoggerMessage(PulsarEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"Pulsar transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(PulsarEventId.TransportReceiverReceiveError, LogLevel.Error,
		"Pulsar transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(PulsarEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"Pulsar transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(PulsarEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"Pulsar transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(PulsarEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"Pulsar transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(PulsarEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"Pulsar transport receiver: message {MessageId} rejected (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(PulsarEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"Pulsar transport receiver: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source}; acknowledged past the poison message.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);

	[LoggerMessage(PulsarEventId.TransportReceiverDisposed, LogLevel.Debug,
		"Pulsar transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(PulsarEventId.TransportReceiverUnsettledOverflow, LogLevel.Warning,
		"Pulsar transport receiver: unsettled cache for {Source} has {Count} entries exceeding expected bounds. Messages may not be getting acknowledged.")]
	private partial void LogUnsettledOverflow(string source, int count);
}
