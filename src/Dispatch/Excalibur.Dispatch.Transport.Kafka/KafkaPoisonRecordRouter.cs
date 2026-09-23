// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Routes a poison record — one that can never become a message, because it is too large or cannot be
/// converted — to the Kafka dead-letter queue, before its offset may be settled.
/// </summary>
/// <remarks>
/// <para>
/// A poison record never reaches a handler, so the dead-letter decorator, which acts on a handler's
/// rejection, never sees it. This is the step that takes a copy of it instead. Shared by every Kafka
/// component that converts records, so the two paths cannot drift apart.
/// </para>
/// <para>
/// <see cref="RouteAsync"/> returning means the caller may settle the offset. When it throws, the
/// dead-letter write did not happen and the caller must NOT settle: the record has to be redelivered and
/// routed again. With no dead-letter queue configured the record is discarded, logged as an Error and
/// counted, because nothing else can hold it.
/// </para>
/// <para>
/// A dead-letter write that can never succeed — a body above the dead-letter producer's size ceiling, a
/// missing topic on a cluster that does not create them, a denied ACL — does NOT keep the record owed.
/// Retrying it forever would stall the partition permanently and silently on a record that is already
/// unprocessable. A body-less tombstone naming the position is written instead, so the drop is recorded
/// rather than invisible.
/// </para>
/// <para>
/// The copy is faithful in its body but not in its headers: Kafka header values are decoded as UTF-8
/// text, so a binary header is not byte-preserved, and Kafka permits repeated header keys where this
/// keeps only the last.
/// </para>
/// </remarks>
internal sealed partial class KafkaPoisonRecordRouter
{
	/// <summary>The cap on a record key carried onto a tombstone.</summary>
	private const int MaxTombstoneKeyChars = 256;

	/// <summary>The cap on the driver's failure text carried onto a tombstone.</summary>
	private const int MaxTombstoneReasonChars = 256;

	/// <summary>
	/// How a retryable dead-letter failure is paced, so a dead-letter topic that is merely not provisioned
	/// yet is retried at a bounded rate rather than at poll speed.
	/// </summary>
	private static readonly BackoffParameters RetryPacing = new()
	{
		BaseDelay = TimeSpan.FromMilliseconds(250),
		MaxDelay = TimeSpan.FromSeconds(5),
		Multiplier = 2.0,
		UseJitter = true,
		JitterFactor = 0.2,
	};

	private readonly IDeadLetterQueueManager? _deadLetter;
	private readonly string _source;
	private readonly ILogger _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Counter<long>? _deadLettered;
	private readonly Counter<long>? _discarded;

	/// <summary>
	/// Consecutive retryable failures per partition. The pacing above is applied to this count, and the
	/// count is cleared the moment a write succeeds, so a single blip does not slow the partition that
	/// follows it.
	/// </summary>
	private readonly ConcurrentDictionary<TopicPartition, int> _consecutiveFailures = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaPoisonRecordRouter"/> class.
	/// </summary>
	/// <param name="deadLetter">The Kafka dead-letter queue, or <see langword="null"/> when none is configured.</param>
	/// <param name="meter">The transport's meter, or <see langword="null"/> to record no metrics.</param>
	/// <param name="source">The source name used in log messages and metric tags.</param>
	/// <param name="logger">The caller's logger.</param>
	/// <param name="timeProvider">The clock used to pace retries, or <see langword="null"/> for the system clock.</param>
	public KafkaPoisonRecordRouter(
		IDeadLetterQueueManager? deadLetter,
		Meter? meter,
		string source,
		ILogger logger,
		TimeProvider? timeProvider = null)
	{
		_deadLetter = deadLetter;
		_source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_timeProvider = timeProvider ?? TimeProvider.System;
		_deadLettered = meter?.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesDeadLettered,
			"{messages}",
			"Total messages routed to dead letter queue");
		_discarded = meter?.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesRejected,
			"{messages}",
			"Total messages rejected");
	}

	/// <summary>
	/// Takes a copy of a poison record, or discards it when no dead-letter queue is configured.
	/// </summary>
	/// <param name="record">The record that cannot become a message.</param>
	/// <param name="reason">Why it cannot.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when the offset may be settled.</returns>
	/// <exception cref="Exception">
	/// The dead-letter write failed for a reason a later attempt could succeed on. The offset must not be
	/// settled; the record is redelivered and routed again.
	/// </exception>
	public async Task RouteAsync(global::Confluent.Kafka.ConsumeResult<string, byte[]> record, Exception reason, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(record);
		ArgumentNullException.ThrowIfNull(reason);

		var errorType = reason.GetType().Name;
		var tags = new TagList
		{
			{ TransportTelemetryConstants.Tags.TransportName, TransportTelemetryConstants.MessagingConventions.Systems.Kafka },
			{ TransportTelemetryConstants.Tags.Source, _source },
			{ TransportTelemetryConstants.Tags.ErrorType, errorType },
		};

		if (_deadLetter is null)
		{
			LogDiscarded(_source, record.Partition.Value, record.Offset.Value, errorType);
			_discarded?.Add(1, tags);
			return;
		}

		try
		{
			_ = await _deadLetter.MoveToDeadLetterAsync(ToTransportMessage(record), errorType, reason, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			if (!IsPermanent(ex))
			{
				// A later attempt could succeed, so the record keeps its claim on the position: the caller
				// does not settle, and the record is redelivered and routed again.
				//
				// Paced before it is handed back. The caller's response to this is an immediate re-poll, so
				// an unprovisioned dead-letter topic or an ACL mid-reload would otherwise be retried at poll
				// speed -- thousands of refused produces a second, an Error line for each, against a fault
				// that is usually seconds from fixing itself. The wait happens here because here is the only
				// place that knows the attempt failed AND that the caller is about to try again.
				LogDeadLetterFailed(_source, record.Partition.Value, record.Offset.Value, ex);

				var attempt = _consecutiveFailures.AddOrUpdate(record.TopicPartition, 1, static (_, count) => count + 1);
				await Task.Delay(ExponentialBackoff.Calculate(attempt, RetryPacing), _timeProvider, cancellationToken)
					.ConfigureAwait(false);

				throw;
			}

			// The write can NEVER succeed, so retrying it forever would stall the partition on a record
			// that is already unprocessable -- silently, at full poll speed. The body is what cannot be
			// written (an oversized record is rejected by the receiver at a threshold above the producer's
			// own ceiling, so by construction it does not fit), so a body-less tombstone is written in its
			// place: the operator learns WHICH position was dropped and why, and the partition keeps moving.
			LogDeadLetterRefused(_source, record.Partition.Value, record.Offset.Value, ex);

			await WriteTombstoneAsync(record, errorType, reason, ex, tags, cancellationToken).ConfigureAwait(false);
			return;
		}

		_ = _consecutiveFailures.TryRemove(record.TopicPartition, out _);
		LogDeadLettered(_source, record.Partition.Value, record.Offset.Value, errorType);
		_deadLettered?.Add(1, tags);
	}

	/// <summary>
	/// Records the position of a poison record whose body could not be written, so it is not dropped silently.
	/// </summary>
	/// <remarks>
	/// Last resort. If the tombstone cannot be written either, the record is discarded with an Error and a
	/// metric rather than stalling the partition: at that point nothing this consumer can do preserves it,
	/// and refusing to move would trade a lost record for a stopped partition AND a lost record.
	/// </remarks>
	private async Task WriteTombstoneAsync(
		global::Confluent.Kafka.ConsumeResult<string, byte[]> record,
		string errorType,
		Exception reason,
		Exception writeFailure,
		TagList tags,
		CancellationToken cancellationToken)
	{
		try
		{
			// BUILT, never copied. Deriving the tombstone from the record would carry the record's headers
			// with it, and a record can be over the producer's ceiling BECAUSE of its headers -- so a copied
			// tombstone is refused for the same reason the body was, in exactly the case this path exists to
			// handle. Every field below is bounded by construction: the position is fixed-width, the key and
			// the driver's message are capped, and nothing else is carried.
			var tombstone = new TransportMessage
			{
				Id = string.Create(
					CultureInfo.InvariantCulture,
					$"{record.Topic}:{record.Partition.Value}:{record.Offset.Value}"),
				Body = ReadOnlyMemory<byte>.Empty,
				Subject = TruncateOrNull(record.Message?.Key, MaxTombstoneKeyChars),
			};

			tombstone.Properties[TransportTelemetryConstants.Tags.Source] = record.Topic;
			tombstone.Properties["dlq_partition"] = record.Partition.Value.ToString(CultureInfo.InvariantCulture);
			tombstone.Properties["dlq_offset"] = record.Offset.Value.ToString(CultureInfo.InvariantCulture);
			tombstone.Properties["dlq_body_dropped"] = bool.TrueString;
			tombstone.Properties["dlq_body_bytes"] = (record.Message?.Value?.Length ?? 0).ToString(CultureInfo.InvariantCulture);
			tombstone.Properties["dlq_body_drop_reason"] = Truncate(writeFailure.Message, MaxTombstoneReasonChars);

			_ = await _deadLetter!.MoveToDeadLetterAsync(tombstone, errorType, reason, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogDiscardedAfterRefusal(_source, record.Partition.Value, record.Offset.Value, ex);
			_discarded?.Add(1, tags);
			return;
		}

		_ = _consecutiveFailures.TryRemove(record.TopicPartition, out _);
		LogTombstoned(_source, record.Partition.Value, record.Offset.Value, record.Message?.Value?.Length ?? 0);
		_deadLettered?.Add(1, tags);
	}

	/// <summary>
	/// Reports whether a dead-letter write failed for a reason no later attempt can succeed on.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Keyed on what is IMPOSSIBLE, not on what is merely BLOCKED, because the two have opposite correct
	/// dispositions. A body above the producer's ceiling can never be written by anyone: no operator action
	/// helps, so the body is dropped and the position is recorded instead.
	/// </para>
	/// <para>
	/// A missing topic, an unknown partition and a denied ACL are NOT that. An operator can fix every one of
	/// them, and several are routinely transient -- metadata still propagating, a leader election in flight,
	/// topic auto-creation lagging, an ACL mid-reload. Treating those as permanent would discard a
	/// consumer's records, at poll speed, because of a configuration fault that is about to be corrected.
	/// They are left on the transient path, where the record keeps its claim on the position and the
	/// partition stops: a stopped partition is a recoverable, visible signal, and dropped records are not.
	/// A fatal producer error is likewise transient here -- the answer to it is to surface and restart, not
	/// to drop what the producer was carrying.
	/// </para>
	/// </remarks>
	internal static bool IsPermanent(Exception exception) =>
		exception is ProduceException<string, byte[]> produce
		&& produce.Error.Code is ErrorCode.MsgSizeTooLarge
			or ErrorCode.InvalidMsgSize
			or ErrorCode.RecordListTooLarge;

	/// <summary>
	/// Caps a value carried onto a tombstone, so the tombstone's size is bounded by construction.
	/// </summary>
	private static string Truncate(string value, int maxChars) =>
		value.Length <= maxChars ? value : value[..maxChars];

	/// <summary>
	/// Caps an optional value carried onto a tombstone, preserving <see langword="null"/> as itself -- a
	/// record with no key is not the same as a record with an empty one.
	/// </summary>
	private static string? TruncateOrNull(string? value, int maxChars) =>
		value is null ? null : Truncate(value, maxChars);

	private static TransportMessage ToTransportMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> record)
	{
		// The raw bytes, not a converted body: the record could not be converted, and a copy that can be
		// reprocessed later has to be the original. The id is the record's position, so a dead-letter
		// handler that is idempotent on the id absorbs the duplicate a redelivery produces.
		var message = new TransportMessage
		{
			Id = string.Create(CultureInfo.InvariantCulture, $"{record.Topic}:{record.Partition.Value}:{record.Offset.Value}"),
			Body = record.Message?.Value ?? [],
			Subject = record.Message?.Key,
		};

		if (record.Message?.Headers is { } headers)
		{
			foreach (var header in headers)
			{
				if (header.GetValueBytes() is { } value)
				{
					message.Properties[header.Key] = Encoding.UTF8.GetString(value);
				}
			}
		}

		message.Properties[TransportTelemetryConstants.Tags.Source] = record.Topic;
		return message;
	}

	[LoggerMessage(KafkaEventId.PoisonRecordDeadLettered, LogLevel.Warning,
		"Kafka transport: the record at {Source} partition {Partition} offset {Offset} could not become a message ({ErrorType}); it was written to the dead-letter queue.")]
	private partial void LogDeadLettered(string source, int partition, long offset, string errorType);

	[LoggerMessage(KafkaEventId.PoisonRecordDiscarded, LogLevel.Error,
		"Kafka transport: the record at {Source} partition {Partition} offset {Offset} could not become a message ({ErrorType}) and no dead-letter queue is configured; it was discarded.")]
	private partial void LogDiscarded(string source, int partition, long offset, string errorType);

	[LoggerMessage(KafkaEventId.PoisonRecordDeadLetterFailed, LogLevel.Error,
		"Kafka transport: the record at {Source} partition {Partition} offset {Offset} could not be written to the dead-letter queue; it is not settled and will be redelivered.")]
	private partial void LogDeadLetterFailed(string source, int partition, long offset, Exception exception);

	[LoggerMessage(KafkaEventId.PoisonRecordDeadLetterRefused, LogLevel.Error,
		"Kafka transport: the dead-letter queue permanently refused the record at {Source} partition {Partition} offset {Offset}; no retry can succeed, so a tombstone is written in place of its body.")]
	private partial void LogDeadLetterRefused(string source, int partition, long offset, Exception exception);

	[LoggerMessage(KafkaEventId.PoisonRecordTombstoned, LogLevel.Error,
		"Kafka transport: the record at {Source} partition {Partition} offset {Offset} was recorded in the dead-letter queue WITHOUT its {BodyBytes}-byte body, which the dead-letter queue would not accept; the body is not recoverable from the dead-letter queue.")]
	private partial void LogTombstoned(string source, int partition, long offset, int bodyBytes);

	[LoggerMessage(KafkaEventId.PoisonRecordDiscardedAfterRefusal, LogLevel.Error,
		"Kafka transport: the record at {Source} partition {Partition} offset {Offset} was discarded; neither it nor a tombstone for it could be written to the dead-letter queue.")]
	private partial void LogDiscardedAfterRefusal(string source, int partition, long offset, Exception exception);
}
