// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// <para>
/// A batch is handed out whole and its members settle in whatever order their handlers finish, so an
/// acknowledgment cannot commit its own offset: a Kafka commit is a single per-partition position meaning
/// "everything below this is dealt with". Settlement is therefore routed through
/// <see cref="KafkaPartitionProgress"/>, which only ever yields the lowest offset still owed — a commit
/// can never cross an offset that is still owed, and never regresses.
/// </para>
/// <para>
/// The receipt handle is stored in <see cref="TransportReceivedMessage.ProviderData"/> under
/// <c>"kafka.receipt_handle"</c>, alongside <c>"kafka.topic"</c>, <c>"kafka.partition"</c>, and the offset.
/// </para>
/// </remarks>
internal sealed partial class KafkaTransportReceiver : ITransportReceiver
{
	private const int DefaultMaxBatchSize = 100;
	private static readonly TimeSpan DefaultMaxBatchWait = TimeSpan.FromMilliseconds(1000);

	private readonly IConsumer<string, byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private readonly bool _decodeConfluentFraming;
	private readonly KafkaPartitionProgress _progress;
	private readonly KafkaOffsetSettler _settler;
	private readonly KafkaPoisonRecordRouter _poison;
	private readonly ConcurrentDictionary<string, Receipt> _receipts = new(StringComparer.Ordinal);

	/// <summary>
	/// Warning threshold for the unsettled receipt cache.
	/// This logs growth; it does not enforce a cache limit.
	/// </summary>
	private const int MaxUnsettledMessages = 10_000;

	/// <summary>
	/// What a receipt handle resolves to: the partition and offset it names, and the assignment
	/// generation the delivery was made under. The generation is what stops an acknowledgment produced
	/// before a rebalance from settling the position of whoever owns the partition now.
	/// </summary>
	private sealed class Receipt(TopicPartition partition, long offset, long generation)
	{
		public TopicPartition Partition { get; } = partition;
		public long Offset { get; } = offset;
		public long Generation { get; } = generation;
		public string Handle { get; } = $"{partition.Topic}:{partition.Partition.Value}:{offset}:{generation}";
	}

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
	/// <param name="progress">
	/// The per-partition progress tracker shared with this consumer's rebalance handlers, so a revoke and
	/// a re-assignment start a new generation that invalidates receipts issued under the previous tenure.
	/// Supplying it is how the two halves are joined; the composed transport does so. When it is
	/// <see langword="null"/> the receiver tracks its own progress — the owed-offset commit rule
	/// still holds in full, but no rebalance handler can reach the tracker, so the generation never
	/// changes and stale-receipt fencing has nothing to fence against.
	/// </param>
	/// <param name="poison">
	/// Where a record that cannot become a message is sent before its offset is settled. When it is
	/// <see langword="null"/> such a record is discarded with an Error log.
	/// </param>
	public KafkaTransportReceiver(
		IConsumer<string, byte[]> consumer,
		string source,
		ILogger<KafkaTransportReceiver> logger,
		int? maxPayloadBytes = PayloadSizeGuard.DefaultMaxPayloadBytes,
		bool decodeConfluentFraming = false,
		KafkaPartitionProgress? progress = null,
		KafkaPoisonRecordRouter? poison = null)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
		_decodeConfluentFraming = decodeConfluentFraming;
		_maxBatchSize = DefaultMaxBatchSize;
		_progress = progress ?? new KafkaPartitionProgress();
		_settler = new KafkaOffsetSettler(_consumer, _progress, Source, _logger);
		_poison = poison ?? new KafkaPoisonRecordRouter(deadLetter: null, meter: null, Source, _logger);
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
	public async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		if (maxMessages <= 0)
		{
			return [];
		}

		var messages = new List<TransportReceivedMessage>();

		// Which offsets this call has handed out. If the batch aborts part-way they never reached the
		// caller, yet they are recorded as dispatched and so are filtered out of a later poll: without
		// this, a mid-batch failure would leave them owed by nobody and stall the partition's commit
		// position permanently. The catch below hands them back.
		var delivered = new List<Receipt>();

		try
		{
			var boundedMaxMessages = Math.Min(maxMessages, _maxBatchSize);

			for (var i = 0; i < boundedMaxMessages && !cancellationToken.IsCancellationRequested; i++)
			{
				var pollTimeout = i == 0 ? DefaultMaxBatchWait : TimeSpan.Zero;
				global::Confluent.Kafka.ConsumeResult<string, byte[]>? consumeResult;
				KafkaDeliveryDecision decision;
				long generation;
				Receipt delivery;
				lock (_progress.OperationGate)
				{
					consumeResult = _consumer.Consume(pollTimeout);
					if (consumeResult?.Message == null)
					{
						break;
					}

					// A requeue seeks the partition backwards, so a poll after one replays offsets that already
					// reached a terminal state. Those must not be handed out a second time.
					decision = _progress.BeginDelivery(consumeResult.TopicPartition, consumeResult.Offset.Value);
					if (decision.AbandonedOffset is { } abandoned)
					{
						LogOwedOffsetAbandoned(Source, consumeResult.Partition.Value, abandoned);
					}

					if (decision.SeekTo is { } seekTo)
					{
						// The fetch position passed an owed offset. Seek back before fetching further. A seek that throws
						// is not fatal to this batch: the next record fetched past the owed offset asks for it again.
						LogOwedOffsetReseek(Source, consumeResult.Partition.Value, seekTo);
						try
						{
							_consumer.Seek(new TopicPartitionOffset(consumeResult.TopicPartition, new Offset(seekTo)));

							// Reported only once the seek returned: the binding waits for it and throws on any failure.
							_progress.NoteSeekIssued(consumeResult.TopicPartition, seekTo);
						}
						catch (KafkaException ex)
						{
							LogUndeliveredBatchSeekFailed(Source, consumeResult.Partition.Value, seekTo, ex);
						}

						continue;
					}

					generation = decision.Generation;
					if (!decision.Deliver)
					{
						LogRedeliveryOfSettledOffsetSkipped(Source, consumeResult.Partition.Value, consumeResult.Offset.Value);
						continue;
					}

					// Recorded the moment delivery begins, before anything else can throw: an offset the tracker holds as
					// dispatched but this batch has not recorded would never be handed back if the batch fails, and would
					// stay owed for the rest of the tenure.
					delivery = new Receipt(consumeResult.TopicPartition, consumeResult.Offset.Value, generation);
					_receipts[delivery.Handle] = delivery;
					delivered.Add(delivery);

				}

				TransportReceivedMessage received;
				try
				{
					lock (_progress.OperationGate)
					{
						received = ConvertToReceivedMessage(consumeResult, delivery);
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// Poison: an oversized or unconvertible record can never be processed, and left owed it would
					// pin the position forever. It is dead-lettered FIRST and settled only after that write
					// returns; settling does not commit past it on its own, because the position only moves once
					// every earlier offset in the partition is terminal too. A dead-letter write that throws
					// leaves it in `delivered`, so the catch below hands it back and it is routed again.
					if (ex is PayloadTooLargeException)
					{
						LogPayloadTooLargeRejected(Source, consumeResult.Message.Value?.Length ?? 0, ex);
					}
					else
					{
						LogConversionFailed(Source, consumeResult.Partition.Value, consumeResult.Offset.Value, ex);
					}

					await _poison.RouteAsync(consumeResult, ex, cancellationToken).ConfigureAwait(false);
					lock (_progress.OperationGate)
					{
						// Poison remains batch-owned through routing and native settlement.
						// A native failure leaves the original entry available to the batch recovery path.
						if (_receipts.TryGetValue(delivery.Handle, out var current) && ReferenceEquals(current, delivery))
						{
							_ = SettleTerminal(consumeResult.TopicPartition, consumeResult.Offset.Value, generation);
							_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(delivery.Handle, delivery));
						}

						_ = delivered.Remove(delivery);
					}
					continue;
				}

				messages.Add(received);
				LogMessageReceived(received.Id, Source);
			}

			return messages;
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			ReturnUndeliveredBatch(delivered);
			throw;
		}
	}

	/// <inheritdoc />
	public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		lock (_progress.OperationGate)
		{
			try
			{
				var receiptHandle = GetReceiptHandle(message);
				var receipt = GetSettlementReceipt(message, receiptHandle);
				SettlePublicTerminal(receiptHandle, receipt);
				LogMessageAcknowledged(message.Id, Source);
				return Task.CompletedTask;
			}
			catch (TransportSettlementException)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogAcknowledgeError(message.Id, Source, ex);
				throw NativeSettlementFailure("acknowledge", ex);
			}
		}
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		lock (_progress.OperationGate)
		{
			try
			{
				var receiptHandle = GetReceiptHandle(message);
				var receipt = GetSettlementReceipt(message, receiptHandle);
				if (requeue)
				{
					var outcome = _progress.TryRequeue(receipt.Partition, receipt.Offset, receipt.Generation, out var seekOffset);
					if (outcome == KafkaRequeueOutcome.NotOutstanding)
					{
						// A failed commit can leave terminal local progress with an uncertain broker outcome.
						// Keep the receipt so a terminal retry can still commit the absolute position.
						throw SettlementRefusal($"Kafka cannot requeue receipt '{receiptHandle}': the offset is already terminal locally.");
					}

					if (outcome != KafkaRequeueOutcome.Requeued)
					{
						_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(receiptHandle, receipt));
						throw SettlementRefusal($"Kafka cannot requeue receipt '{receiptHandle}': the delivery is not owned by the current assignment.");
					}

					// Native failure keeps both the receipt and the owed offset available for retry.
					_consumer.Seek(new TopicPartitionOffset(receipt.Partition, new Offset(seekOffset)));
					_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(receiptHandle, receipt));
					LogMessageRequeueSeek(Source, receipt.Partition.Partition.Value, seekOffset);
					LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
				}
				else
				{
					SettlePublicTerminal(receiptHandle, receipt);
					LogMessageRejected(message.Id, Source, reason ?? "no reason");
				}

				return Task.CompletedTask;
			}
			catch (TransportSettlementException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw NativeSettlementFailure("reject", ex);
			}
		}
	}

	private Receipt GetSettlementReceipt(TransportReceivedMessage message, string receiptHandle)
	{
		if (!message.ProviderData.TryGetValue("kafka.receipt_entry", out var capability)
			|| capability is not Receipt receipt
			|| !_receipts.TryGetValue(receiptHandle, out var current)
			|| !ReferenceEquals(receipt, current))
		{
			throw SettlementRefusal($"Kafka cannot settle receipt '{receiptHandle}': it is not in the receipt cache.");
		}

		return receipt;
	}

	private void SettlePublicTerminal(string receiptHandle, Receipt receipt)
	{
		if (!SettleTerminal(receipt.Partition, receipt.Offset, receipt.Generation))
		{
			_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(receiptHandle, receipt));
			throw SettlementRefusal($"Kafka cannot settle receipt '{receiptHandle}': its assignment has ended.");
		}

		_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(receiptHandle, receipt));
	}

	private static TransportSettlementException SettlementRefusal(string message) => new(message)
	{
		TransportName = "Kafka",
		RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
		Retryability = SettlementRetryability.Permanent,
	};

	private static TransportSettlementException NativeSettlementFailure(string operation, Exception cause) =>
		new($"Kafka could not {operation} the message.", cause)
		{
			TransportName = "Kafka",
			RedeliveryExpectation = TransportRedeliveryExpectation.Unspecified,
			Retryability = SettlementRetryability.Unspecified,
		};

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IConsumer<string, byte[]>))
		{
			return _consumer;
		}

		if (serviceType == typeof(KafkaPartitionProgress))
		{
			return _progress;
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

	/// <summary>
	/// Hands back still-outstanding original deliveries from a batch that failed before returning,
	/// so the next poll redelivers them instead of the partition stalling on work nobody holds.
	/// </summary>
	/// <remarks>
	/// Best-effort by construction: this runs while an exception is in flight, and a seek that fails here
	/// must not replace the original fault. Already-terminal progress is not rolled back; recovery only
	/// attempts to requeue deliveries that remain outstanding and owned by this failed batch.
	/// </remarks>
	private void ReturnUndeliveredBatch(List<Receipt> delivered)
	{
		lock (_progress.OperationGate)
		{
			foreach (var receipt in delivered)
			{
				var partition = receipt.Partition;
				var offset = receipt.Offset;
				var generation = receipt.Generation;
				try
				{
					if (!_receipts.TryGetValue(receipt.Handle, out var current) || !ReferenceEquals(current, receipt))
					{
						// Only the exact original cache entry proves this failed batch still owns the delivery.
						continue;
					}

					_ = _receipts.TryRemove(new KeyValuePair<string, Receipt>(receipt.Handle, receipt));
					if (_progress.TryRequeue(partition, offset, generation, out var seekOffset) == KafkaRequeueOutcome.Requeued)
					{
						_consumer.Seek(new TopicPartitionOffset(partition, new Offset(seekOffset)));
					}
				}
				catch (KafkaException ex)
				{
					LogUndeliveredBatchSeekFailed(Source, partition.Partition.Value, offset, ex);
				}
			}
		}
	}

	/// <summary>
	/// Settles an offset as terminal and commits the resulting position, if it moved.
	/// </summary>
	/// <remarks>
	/// The commit offset comes from <see cref="KafkaPartitionProgress"/> and is the lowest offset still owed
	/// (or one past the highest fetched offset when nothing is), never this message's own offset plus one.
	/// The commit is recorded only after the broker accepts it, so a throwing commit is retried by the next
	/// settle.
	/// </remarks>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset that reached a terminal state.</param>
	/// <param name="generation">The generation the delivery was made under.</param>
	/// <returns>
	/// <see langword="true"/> when the settlement was accepted (whether or not the position moved far enough
	/// to commit); <see langword="false"/> when it was refused because it carries a generation from before
	/// a rebalance, and so belongs to a tenure that no longer owns the partition.
	/// </returns>
	private bool SettleTerminal(TopicPartition partition, long offset, long generation) =>
		_settler.SettleTerminal(partition, offset, generation);

	private TransportReceivedMessage ConvertToReceivedMessage(
		global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult,
		Receipt receipt)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE it is copied into the
		// materialized message below. Fail-closed — throws PayloadTooLargeException, which the receive
		// loop catches to commit past (skip) the poison message; it never truncates or silently drops.
		PayloadSizeGuard.EnsureWithinLimit(consumeResult.Message.Value?.Length ?? 0, _maxPayloadBytes);

		var receiptHandle = receipt.Handle;

		// Warn if unsettled message count is growing beyond expected bounds.
		if (_receipts.Count > MaxUnsettledMessages)
		{
			LogOffsetCacheOverflow(Source, _receipts.Count);
		}

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (consumeResult.Message.Headers is not null)
		{
			foreach (var header in consumeResult.Message.Headers)
			{
				// A Kafka header may legally carry a null value. It has nothing to convey, so it is skipped
				// rather than decoded; decoding it throws, and would make an ordinary message unreadable.
				if (header.GetValueBytes() is not { } value)
				{
					continue;
				}

				properties[header.Key] = Encoding.UTF8.GetString(value);
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
				["kafka.receipt_entry"] = receipt,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("kafka.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}

		throw SettlementRefusal("Message does not contain a Kafka receipt handle in ProviderData.");
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

	[LoggerMessage(KafkaEventId.TransportReceiverRedeliveryOfSettledOffsetSkipped, LogLevel.Debug,
		"Kafka transport receiver: {Source} partition {Partition} offset {Offset} replayed by a requeue seek but already terminal; not dispatched again.")]
	private partial void LogRedeliveryOfSettledOffsetSkipped(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverStaleGenerationIgnored, LogLevel.Warning,
		"Kafka transport receiver: ignored a settlement for {Source} partition {Partition} offset {Offset} issued under a previous assignment generation; the partition has since been revoked or reassigned.")]
	private partial void LogStaleGenerationSettleIgnored(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverUndeliveredBatchSeekFailed, LogLevel.Warning,
		"Kafka transport receiver: could not seek {Source} partition {Partition} back to offset {Offset} after a failed batch; redelivery waits for the next rebalance or restart.")]
	private partial void LogUndeliveredBatchSeekFailed(string source, int partition, long offset, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverRequeueSeek, LogLevel.Debug,
		"Kafka transport receiver: sought {Source} partition {Partition} back to offset {Offset} to redeliver requeued work.")]
	private partial void LogMessageRequeueSeek(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverRequeueOfSettledOffsetIgnored, LogLevel.Debug,
		"Kafka transport receiver: ignored a requeue for {Source} partition {Partition} offset {Offset}; this receiver had already settled it.")]
	private partial void LogRequeueOfSettledOffsetIgnored(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverOwedOffsetReseek, LogLevel.Warning,
		"Kafka transport receiver: sought {Source} partition {Partition} back to offset {Offset}, which is owed a redelivery the fetch position had passed.")]
	private partial void LogOwedOffsetReseek(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverOwedOffsetAbandoned, LogLevel.Warning,
		"Kafka transport receiver: released owed offset {Offset} on {Source} partition {Partition}; two seeks back to it each returned a later record, so it no longer exists in the log (removed by compaction) and cannot be redelivered.")]
	private partial void LogOwedOffsetAbandoned(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverConversionFailed, LogLevel.Error,
		"Kafka transport receiver: could not convert the record at {Source} partition {Partition} offset {Offset} into a message.")]
	private partial void LogConversionFailed(string source, int partition, long offset, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverPayloadTooLarge, LogLevel.Warning,
		"Kafka transport receiver: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
