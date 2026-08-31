// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="ITransportReceiver"/>.
/// Uses Confluent.Kafka's <see cref="IConsumer{TKey,TValue}"/> for native message consumption.
/// </summary>
/// <remarks>
/// Acknowledgment commits the next offset (current + 1) via the consumer.
/// The receipt handle is stored in <see cref="TransportReceivedMessage.ProviderData"/>
/// as <c>"kafka.topic"</c>, <c>"kafka.partition"</c>, and <c>"kafka.offset"</c>.
/// </remarks>
internal sealed partial class KafkaTransportReceiver : ITransportReceiver
{
	private const int DefaultMaxBatchSize = 100;
	private static readonly TimeSpan DefaultMaxBatchWait = TimeSpan.FromMilliseconds(1000);

	private readonly IConsumer<string, byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private readonly bool _decodeConfluentFraming;
	private readonly ConcurrentDictionary<string, TopicPartitionOffset> _offsetCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Maximum unsettled messages tracked in the offset cache.
	/// Prevents unbounded memory growth if messages are received but never settled.
	/// </summary>
	private const int MaxUnsettledMessages = 10_000;

	private volatile bool _disposed;
	private readonly int _maxBatchSize;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportReceiver"/> class.
	/// </summary>
	/// <param name="consumer">The Kafka consumer instance.</param>
	/// <param name="source">The source topic name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound-payload length, in bytes, enforced before the body is materialized, or
	/// <see langword="null"/> to opt out (unbounded). Defaults to 4 MiB.
	/// </param>
	/// <param name="decodeConfluentFraming">
	/// When <see langword="true"/> (a Confluent Schema Registry-configured transport), a Confluent-framed
	/// inbound payload (magic byte + schema id) has its 5-byte header stripped so the downstream deserializer
	/// receives the raw payload. The .NET message type is carried in the <c>message-type</c> header, so the
	/// schema id itself is not needed to deserialize. Non-framed payloads are passed through untouched.
	/// </param>
	public KafkaTransportReceiver(
		IConsumer<string, byte[]> consumer,
		string source,
		ILogger<KafkaTransportReceiver> logger,
		int? maxPayloadBytes = PayloadSizeGuard.DefaultMaxPayloadBytes,
		bool decodeConfluentFraming = false)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
		_decodeConfluentFraming = decodeConfluentFraming;
		_maxBatchSize = DefaultMaxBatchSize;
	}

	/// <summary>
	/// Materializes the transport body, stripping Confluent Schema Registry framing when this transport is
	/// schema-registry-configured and the payload carries the Confluent wire-format header.
	/// </summary>
	private byte[] MaterializeBody(byte[]? value)
	{
		if (value is null || value.Length == 0)
		{
			return [];
		}

		if (_decodeConfluentFraming && ConfluentWireFormat.TryReadSchemaId(value, out _))
		{
			return ConfluentWireFormat.GetPayload(value).ToArray();
		}

		return value;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			if (maxMessages <= 0)
			{
				return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([]);
			}

			var messages = new List<TransportReceivedMessage>();
			var boundedMaxMessages = Math.Min(maxMessages, _maxBatchSize);

			for (var i = 0; i < boundedMaxMessages && !cancellationToken.IsCancellationRequested; i++)
			{
				var pollTimeout = i == 0 ? DefaultMaxBatchWait : TimeSpan.Zero;
				var consumeResult = _consumer.Consume(pollTimeout);
				if (consumeResult?.Message == null)
				{
					break;
				}

				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(consumeResult);
				}
				catch (PayloadTooLargeException ex)
				{
					// Poison-message guard: an oversized payload can never be processed and, left uncommitted,
					// Kafka would redeliver it forever — a poison loop that stalls the partition. Commit past
					// the offending message (offset + 1) to skip it — mirrors the non-requeue reject path — and
					// continue the batch, rather than letting the throw abort the batch and orphan the message.
					LogPayloadTooLargeRejected(Source, consumeResult.Message.Value?.Length ?? 0, ex);
					_consumer.Commit([new TopicPartitionOffset(
						consumeResult.Topic,
						consumeResult.Partition,
						consumeResult.Offset + 1)]);
					continue;
				}

				messages.Add(received);
				LogMessageReceived(received.Id, Source);
			}

			return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(messages);
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);
		try
		{
			// Commit THEN TryRemove: if Commit throws, the offset stays in cache for retry.
			// Previous order (TryRemove then Commit) lost the offset on Commit failure.
			if (_offsetCache.TryGetValue(receiptHandle, out var tpo))
			{
				_consumer.Commit([new TopicPartitionOffset(tpo.Topic, tpo.Partition, tpo.Offset + 1)]);
				_offsetCache.TryRemove(receiptHandle, out _);
				LogMessageAcknowledged(message.Id, Source);
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with receipt handle '{receiptHandle}' not found in offset cache. It may have already been processed.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(message.Id, Source, ex);
			throw;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);

		if (requeue)
		{
			// Don't commit — Kafka will redeliver after session timeout
			_ = _offsetCache.TryRemove(receiptHandle, out _);
			LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
		}
		else
		{
			// Commit THEN TryRemove to skip this message (DLQ routing handled by decorator)
			if (_offsetCache.TryGetValue(receiptHandle, out var tpo))
			{
				_consumer.Commit([new TopicPartitionOffset(tpo.Topic, tpo.Partition, tpo.Offset + 1)]);
				_offsetCache.TryRemove(receiptHandle, out _);
			}

			LogMessageRejected(message.Id, Source, reason ?? "no reason");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IConsumer<string, byte[]>))
		{
			return _consumer;
		}

		return null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private TransportReceivedMessage ConvertToReceivedMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE it is copied into the
		// materialized message below. Fail-closed — throws PayloadTooLargeException, which the receive
		// loop catches to commit past (skip) the poison message; it never truncates or silently drops.
		PayloadSizeGuard.EnsureWithinLimit(consumeResult.Message.Value?.Length ?? 0, _maxPayloadBytes);

		var receiptHandle = $"{consumeResult.Topic}:{consumeResult.Partition.Value}:{consumeResult.Offset.Value}";
		_offsetCache[receiptHandle] = consumeResult.TopicPartitionOffset;

		// Warn if unsettled message count is growing beyond expected bounds.
		if (_offsetCache.Count > MaxUnsettledMessages)
		{
			LogOffsetCacheOverflow(Source, _offsetCache.Count);
		}

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (consumeResult.Message.Headers is not null)
		{
			foreach (var header in consumeResult.Message.Headers)
			{
				properties[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
			}
		}

		var contentType = properties.TryGetValue("content-type", out var ct) ? ct as string : null;
		var messageType = properties.TryGetValue("message-type", out var mt) ? mt as string : null;
		var correlationId = properties.TryGetValue(OutboxHeaderNames.CorrelationId, out var ci) ? ci as string : null;
		var messageId = properties.TryGetValue("message-id", out var mi) ? mi as string : null;

		return new TransportReceivedMessage
		{
			Id = messageId ?? consumeResult.Message.Key ?? receiptHandle,
			Body = MaterializeBody(consumeResult.Message.Value),
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			Source = Source,
			PartitionKey = consumeResult.Message.Key,
			EnqueuedAt = consumeResult.Message.Timestamp.UtcDateTime != DateTime.MinValue
				? new DateTimeOffset(consumeResult.Message.Timestamp.UtcDateTime, TimeSpan.Zero)
				: DateTimeOffset.UtcNow,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["kafka.topic"] = consumeResult.Topic,
				["kafka.partition"] = consumeResult.Partition.Value,
				[TransportOrderingMetadata.KafkaOffsetKey] = consumeResult.Offset.Value,
				["kafka.receipt_handle"] = receiptHandle,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("kafka.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}

		throw new InvalidOperationException("Message does not contain a Kafka receipt handle in ProviderData.");
	}

	[LoggerMessage(KafkaEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportReceiverReceiveError, LogLevel.Error,
		"Kafka transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"Kafka transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"Kafka transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} rejected (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(KafkaEventId.TransportReceiverDisposed, LogLevel.Debug,
		"Kafka transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(KafkaEventId.TransportReceiverOffsetCacheOverflow, LogLevel.Warning,
		"Kafka transport receiver: offset cache for {Source} has {Count} unsettled entries exceeding expected bounds. Messages may not be getting acknowledged.")]
	private partial void LogOffsetCacheOverflow(string source, int count);

	[LoggerMessage(KafkaEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"Kafka transport receiver: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization; committed past the poison message.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
