// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="ITransportSubscriber"/>.
/// Uses Confluent.Kafka's <see cref="IConsumer{TKey,TValue}"/> for push-based message delivery
/// via a consume loop.
/// </summary>
/// <remarks>
/// <para>
/// The consumer poll loop calls <see cref="IConsumer{TKey,TValue}.Consume(CancellationToken)"/> and invokes the
/// handler callback for each received message. Message settlement is determined by the returned <see cref="MessageAction"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> commits the offset (auto-commit or explicit).</item>
/// <item><see cref="MessageAction.Reject"/> logs the rejection (offset advances).</item>
/// <item><see cref="MessageAction.Requeue"/> seeks back to the current offset for redelivery.</item>
/// </list>
/// <para>
/// A handler that <em>throws</em> reaches no decision, so the message is sought back for redelivery
/// exactly as <see cref="MessageAction.Requeue"/> would — never left in place. Kafka offsets are
/// positional, so a message left unsettled is not merely delayed: the next settled message commits a
/// position past it and it is never redelivered. Consumers should therefore expect a throwing handler
/// to see the same message again, and must route poison messages through a terminal
/// <see cref="MessageAction.Reject"/> rather than by throwing.
/// </para>
/// <para>
/// Provider-specific data is stored in <see cref="TransportReceivedMessage.ProviderData"/>:
/// <c>"kafka.partition"</c>, <c>"kafka.offset"</c>, and <c>"kafka.topic"</c>.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
[RequiresDynamicCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
internal sealed partial class KafkaTransportSubscriber : ITransportSubscriber
{
	private readonly IConsumer<string, byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private readonly bool _decodeConfluentFraming;
	private readonly KafkaPartitionProgress _progress;
	private readonly KafkaOffsetSettler _settler;
	private readonly KafkaPoisonRecordRouter _poison;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportSubscriber"/> class.
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
	/// The partition progress tracker shared with the consumer's rebalance handlers. Settlements go through
	/// it so the committed offset is the lowest offset still owed and never passes a message that
	/// has not reached a decision. Defaults to a private tracker when none is supplied.
	/// </param>
	/// <param name="poison">
	/// Where a record that cannot become a message is sent before its offset is settled. When it is
	/// <see langword="null"/> such a record is discarded with an Error log.
	/// </param>
	public KafkaTransportSubscriber(
		IConsumer<string, byte[]> consumer,
		string source,
		ILogger<KafkaTransportSubscriber> logger,
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
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		// Use a separate CTS for in-flight message processing so that handlers can
		// complete commit even after the subscription token is cancelled.
		using var messageProcessingCts = new CancellationTokenSource();

		_consumer.Subscribe(Source);
		LogSubscriptionStarted(Source);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult;
				try
				{
					consumeResult = _consumer.Consume(cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				if (consumeResult?.Message == null)
				{
					continue;
				}

				// A seek-back replays every offset from the seek point, not only the one that was requeued. An
				// offset that already reached a decision must not be handed to the handler a second time.
				var decision = _progress.BeginDelivery(consumeResult.TopicPartition, consumeResult.Offset.Value);
				if (decision.AbandonedOffset is { } abandoned)
				{
					LogOwedOffsetAbandoned(Source, consumeResult.Partition.Value, abandoned);
				}

				if (decision.SeekTo is { } seekTo)
				{
					// The fetch position passed an owed offset: a seek back was lost or overtaken. Seek again before
					// fetching further. A seek that throws here is asked for again by the next fetch.
					LogOwedOffsetReseek(Source, consumeResult.Partition.Value, seekTo);
					try
					{
						_consumer.Seek(new TopicPartitionOffset(consumeResult.TopicPartition, new Offset(seekTo)));

						// Reported only once the seek returned: the binding waits for it and throws on any failure.
						_progress.NoteSeekIssued(consumeResult.TopicPartition, seekTo);
					}
					catch (KafkaException ex)
					{
						LogRedeliverySeekFailed(consumeResult.TopicPartitionOffset.ToString(), Source, ex);
					}

					continue;
				}

				var generation = decision.Generation;
				if (!decision.Deliver)
				{
					continue;
				}

				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(consumeResult);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					if (ex is PayloadTooLargeException)
					{
						LogPayloadTooLargeRejected(Source, consumeResult.Message.Value?.Length ?? 0, ex);
					}
					else
					{
						LogConversionFailed(Source, consumeResult.Partition.Value, consumeResult.Offset.Value, ex);
					}

					await RoutePoisonRecordAsync(consumeResult, generation, ex, messageProcessingCts.Token).ConfigureAwait(false);
					continue;
				}

				LogMessageReceived(received.Id, Source);

				MessageAction action;
				try
				{
					action = await handler(received, messageProcessingCts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// Only THIS subscription's token ends the loop. A handler that cancels work on a token it
					// owns -- a per-attempt timeout, an HTTP client's own deadline -- throws an
					// OperationCanceledException carrying THAT token, which says nothing about whether the
					// subscription should stop. Testing the exception's own token instead ended the whole
					// subscription silently on a routine handler timeout, leaving the message neither settled
					// nor redelivered. Every other cancellation falls through to the handler-failure branch
					// below and is sought back, because a handler that did not finish reached no decision.
					break;
				}
				catch (Exception ex)
				{
					// A throwing handler reached no decision, so the message must go back — not forward.
					// Leaving it here would settle nothing AND redeliver nothing, and because Kafka offsets
					// are positional the next message's commit would name a position past this one and
					// subsume it. Seeking is the same mechanism the Requeue branch uses, and it is what every
					// other transport does in this position (abandon, nack, or reset the visibility timeout).
					LogError(received.Id, Source, ex);

					try
					{
						SeekBackForRedelivery(consumeResult, generation);
						LogMessageRequeued(received.Id, Source);
					}
					catch (Exception seekEx)
					{
						// Usually the partition was revoked while the handler ran, in which case the new owner
						// resumes from the last COMMITTED offset and redelivers this message anyway. Otherwise the
						// offset is already recorded as owed, so no commit can pass it; it is redelivered when the
						// partition moves or the consumer restarts. Logged, and it must not also kill the loop.
						LogRedeliverySeekFailed(received.Id, Source, seekEx);
					}

					continue;
				}

				// The handler reached a decision, so everything below is settlement, not handling. A failure
				// here must not be treated as a failed handler: the work is done, and seeking back would run it a
				// second time in this process on top of whatever the broker redelivers.
				Settle(action, received, consumeResult, generation);
			}
		}
		finally
		{
			// Cancel in-flight message processing
			await messageProcessingCts.CancelAsync().ConfigureAwait(false);

			_consumer.Close();
			LogSubscriptionStopped(Source);
		}
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
		_consumer.Dispose();
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Dead-letters a record that can never become a message, then settles it as terminal.
	/// </summary>
	/// <remarks>
	/// It never reaches a handler, so the dead-letter decorator never sees it; this takes the copy instead.
	/// It is settled only after the dead-letter write returns. A write that fails leaves it unsettled and
	/// seeks back, so it is redelivered and routed again rather than skipped.
	/// </remarks>
	private async Task RoutePoisonRecordAsync(
		global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult,
		long generation,
		Exception reason,
		CancellationToken cancellationToken)
	{
		try
		{
			await _poison.RouteAsync(consumeResult, reason, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			try
			{
				SeekBackForRedelivery(consumeResult, generation);
			}
			catch (Exception seekEx)
			{
				// Still owed, so no commit can pass it; the next fetch past it asks for the seek again.
				LogRedeliverySeekFailed(consumeResult.TopicPartitionOffset.ToString(), Source, seekEx);
			}

			return;
		}

		_ = _settler.SettleTerminal(consumeResult.TopicPartition, consumeResult.Offset.Value, generation);
	}

	/// <summary>
	/// Carries out the decision a handler returned.
	/// </summary>
	/// <remarks>
	/// A settlement that throws after an acknowledgement or rejection leaves the outcome UNKNOWN in the sense of
	/// the transport settlement contract: the message was processed, and the broker may or may not redeliver
	/// it after a restart. The tracker keeps the position ahead of the last accepted commit, so the next
	/// settle on the partition commits it again; nothing is requeued, because the work is not owed.
	/// </remarks>
	/// <param name="action">The handler's decision.</param>
	/// <param name="received">The message the decision is about.</param>
	/// <param name="consumeResult">The consumed record the message came from.</param>
	/// <param name="generation">The assignment generation the message was delivered under.</param>
	private void Settle(
		MessageAction action,
		TransportReceivedMessage received,
		global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult,
		long generation)
	{
		try
		{
			switch (action)
			{
				case MessageAction.Acknowledge:
					_ = _settler.SettleTerminal(consumeResult.TopicPartition, consumeResult.Offset.Value, generation);
					LogMessageAcknowledged(received.Id, Source);
					break;

				case MessageAction.Reject:
					// Terminal: the position moves past it only once everything before it is terminal too.
					// DLQ routing is handled by the decorator.
					_ = _settler.SettleTerminal(consumeResult.TopicPartition, consumeResult.Offset.Value, generation);
					LogMessageRejected(received.Id, Source);
					break;

				case MessageAction.Requeue:
					SeekBackForRedelivery(consumeResult, generation);
					LogMessageRequeued(received.Id, Source);
					break;
			}
		}
		catch (Exception ex) when (action is MessageAction.Requeue && ex is not OperationCanceledException)
		{
			LogRedeliverySeekFailed(received.Id, Source, ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Any failure, not only a broker error: this used to sit inside the handler's catch-all, and a
			// disposed consumer or a store rejected locally must not end the subscription loop either.
			LogSettlementCommitFailed(received.Id, Source, ex);
		}
	}

	/// <summary>
	/// Hands a message back for redelivery by seeking its partition to the earliest offset still owed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The seek goes through the progress tracker rather than straight to this message's offset. A bare
	/// seek would leave the offset recorded as in someone's hands, so the tracker would refuse the replay and
	/// the message would be dropped instead of redelivered. Requeuing it first moves it to the owed set, so
	/// the replay is admitted and the committed position still cannot pass it.
	/// </para>
	/// <para>
	/// The tracker declines in two cases, and neither seeks. When the partition's assignment has changed
	/// since this message was fetched, this consumer no longer owns it; the offset was never settled, so the
	/// partition's current owner resumes before it and the message is delivered again there. When the
	/// offset is not owed at all — already settled — there is nothing to redeliver.
	/// </para>
	/// </remarks>
	/// <param name="consumeResult">The message to redeliver.</param>
	/// <param name="generation">The assignment generation the message was delivered under.</param>
	private void SeekBackForRedelivery(global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult, long generation)
	{
		if (_progress.TryRequeue(consumeResult.TopicPartition, consumeResult.Offset.Value, generation, out var seekOffset)
			== KafkaRequeueOutcome.Requeued)
		{
			_consumer.Seek(new TopicPartitionOffset(consumeResult.TopicPartition, new Offset(seekOffset)));
		}
	}

	private TransportReceivedMessage ConvertToReceivedMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE the body is materialized below.
		// Fail-closed — throws PayloadTooLargeException, which the consume loop catches to commit past
		// (skip) the poison message; it never truncates or silently drops.
		PayloadSizeGuard.EnsureWithinLimit(consumeResult.Message.Value?.Length ?? 0, _maxPayloadBytes);

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
			Id = messageId ?? consumeResult.Message.Key ?? $"{consumeResult.Topic}:{consumeResult.Partition.Value}:{consumeResult.Offset.Value}",
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
				["kafka.partition"] = consumeResult.Partition.Value,
				[TransportOrderingMetadata.KafkaOffsetKey] = consumeResult.Offset.Value,
				["kafka.topic"] = consumeResult.Topic,
			},
		};
	}

	[LoggerMessage(KafkaEventId.TransportSubscriberStarted, LogLevel.Information,
		"Kafka transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"Kafka transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} requeued (seek back) from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberError, LogLevel.Error,
		"Kafka transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSubscriberRedeliverySeekFailed, LogLevel.Warning,
		"Kafka transport subscriber: could not seek back to redeliver message {MessageId} from {Source}; it stays owed, so no commit passes it, and it is redelivered when the partition is reassigned or the consumer restarts.")]
	private partial void LogRedeliverySeekFailed(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSubscriberSettlementCommitFailed, LogLevel.Warning,
		"Kafka transport subscriber: message {MessageId} from {Source} was handled but settling its offset failed; the next settle on the partition commits the position again, and a restart before then may redeliver it.")]
	private partial void LogSettlementCommitFailed(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSubscriberStopped, LogLevel.Information,
		"Kafka transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"Kafka transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberOwedOffsetReseek, LogLevel.Warning,
		"Kafka transport subscriber: sought {Source} partition {Partition} back to offset {Offset}, which is owed a redelivery the fetch position had passed.")]
	private partial void LogOwedOffsetReseek(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportSubscriberOwedOffsetAbandoned, LogLevel.Warning,
		"Kafka transport subscriber: released owed offset {Offset} on {Source} partition {Partition}; two seeks back to it each returned a later record, so it no longer exists in the log (removed by compaction) and cannot be redelivered.")]
	private partial void LogOwedOffsetAbandoned(string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportSubscriberConversionFailed, LogLevel.Error,
		"Kafka transport subscriber: could not convert the record at {Source} partition {Partition} offset {Offset} into a message.")]
	private partial void LogConversionFailed(string source, int partition, long offset, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSubscriberPayloadTooLarge, LogLevel.Warning,
		"Kafka transport subscriber: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
