// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Consumes messages from Kafka dead letter topics for inspection and reprocessing.
/// </summary>
/// <remarks>
/// <para>
/// Uses a dedicated consumer group (<see cref="KafkaDeadLetterOptions.ConsumerGroupId"/>) separate
/// from the main application consumer group. Messages are consumed from DLQ topics and converted to
/// <see cref="DeadLetterMessage"/> instances with metadata extracted from Kafka headers.
/// </para>
/// </remarks>
internal sealed partial class KafkaDeadLetterConsumer : IDisposable
{
	private readonly IConsumer<string, byte[]> _consumer;
	private readonly KafkaDeadLetterOptions _dlqOptions;
	private readonly ILogger<KafkaDeadLetterConsumer> _logger;
	private string? _subscribedTopic;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaDeadLetterConsumer"/> class.
	/// </summary>
	/// <param name="kafkaOptions"> The Kafka connection options. </param>
	/// <param name="dlqOptions"> The dead letter queue configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public KafkaDeadLetterConsumer(
		IOptions<KafkaOptions> kafkaOptions,
		IOptions<KafkaDeadLetterOptions> dlqOptions,
		ILogger<KafkaDeadLetterConsumer> logger)
		: this(BuildConsumer(kafkaOptions, dlqOptions), dlqOptions, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaDeadLetterConsumer"/> class around an
	/// already-constructed consumer.
	/// </summary>
	/// <param name="consumer"> The Kafka consumer this instance owns and disposes. </param>
	/// <param name="dlqOptions"> The dead letter queue configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <remarks>
	/// Exists so a test can drive this class without a broker. The public constructor builds a live
	/// librdkafka consumer as a side effect of construction, which means merely NEWING THIS TYPE opens
	/// network connections -- and against an absent broker those connections retry on background threads
	/// for the life of the process. Under a parallel test run that accumulates handles until the native
	/// library aborts the host AFTER every test has passed, so the failure surfaces as an exit code with
	/// no failing test attached to it.
	///
	/// It also makes the assertions honest. A "returns no messages" test that runs against no broker at
	/// all passes for the wrong reason: it cannot distinguish a correctly-drained topic from a consumer
	/// that never connected. Handing in the consumer lets the test state which one it is.
	/// </remarks>
	internal KafkaDeadLetterConsumer(
		IConsumer<string, byte[]> consumer,
		IOptions<KafkaDeadLetterOptions> dlqOptions,
		ILogger<KafkaDeadLetterConsumer> logger)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		_dlqOptions = dlqOptions?.Value ?? throw new ArgumentNullException(nameof(dlqOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		LogConsumerStarted(_logger, _dlqOptions.ConsumerGroupId);
	}

	/// <summary>Builds the live consumer used by the public constructor.</summary>
	/// <param name="kafkaOptions"> The Kafka connection options. </param>
	/// <param name="dlqOptions"> The dead letter queue configuration options. </param>
	/// <returns> A connected Kafka consumer. </returns>
	private static IConsumer<string, byte[]> BuildConsumer(
		IOptions<KafkaOptions> kafkaOptions,
		IOptions<KafkaDeadLetterOptions> dlqOptions)
	{
		ArgumentNullException.ThrowIfNull(kafkaOptions);
		ArgumentNullException.ThrowIfNull(dlqOptions);

		// Built through the shared builder rather than by hand: the hand-built configuration carried
		// none of the connection settings in AdditionalConfig, so a dead-letter drain against a
		// TLS-only broker connected -- or tried to -- in plaintext regardless of how the transport
		// itself was configured. The group and offset policy are dead-letter-specific and override it.
		var config = KafkaConsumerConfigBuilder.Build(kafkaOptions.Value);
		config.GroupId = dlqOptions.Value.ConsumerGroupId;
		config.AutoOffsetReset = AutoOffsetReset.Earliest;
		config.EnableAutoCommit = false;

		return new ConsumerBuilder<string, byte[]>(config).Build();
	}

	/// <summary>
	/// Reads up to <paramref name="maxMessages"/> from the specified dead letter topic WITHOUT advancing
	/// the committed position.
	/// </summary>
	/// <remarks>
	/// Reading does not consume: the group's committed position moves only when the caller reports what it
	/// has taken ownership of, through <see cref="CommitProcessed"/>. A dead letter queue is the last copy
	/// of a message, so a caller that throws, crashes or discards the returned list must see those messages
	/// again on the next read rather than lose them. Committing here would advance the group past every
	/// message returned before the caller had done anything with any of them.
	/// </remarks>
	/// <param name="dlqTopic"> The dead letter topic to consume from. </param>
	/// <param name="maxMessages"> The maximum number of messages to consume. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A list of dead letter messages with metadata. </returns>
	public IReadOnlyList<DeadLetterMessage> Consume(
		string dlqTopic,
		int maxMessages,
		CancellationToken cancellationToken)
	{
		EnsureSubscribed(dlqTopic);
		var messages = new List<DeadLetterMessage>();

		while (messages.Count < maxMessages)
		{
			var result = _consumer.Consume(_dlqOptions.ConsumeTimeout);
			if (result is null || result.IsPartitionEOF)
			{
				break;
			}

			cancellationToken.ThrowIfCancellationRequested();

			var dlqMessage = ConvertToDeadLetterMessage(result);
			messages.Add(dlqMessage);
		}

		LogMessagesRetrieved(_logger, messages.Count, dlqTopic);

		return messages;
	}

	/// <summary>
	/// Advances the committed position past the messages the caller has taken ownership of.
	/// </summary>
	/// <remarks>
	/// Commits one explicit position per partition: one past the highest offset among the processed
	/// messages of that partition. Nothing else is committed, so a message the caller did not process is
	/// read again. Messages carrying no Kafka position are ignored, since there is nothing to commit for
	/// them.
	/// </remarks>
	/// <param name="processed"> The messages the caller has persisted or acted on. </param>
	public void CommitProcessed(IEnumerable<DeadLetterMessage> processed)
	{
		ArgumentNullException.ThrowIfNull(processed);

		var highestByPartition = new Dictionary<TopicPartition, long>();
		foreach (var message in processed)
		{
			if (!TryGetPosition(message, out var topicPartition, out var offset))
			{
				continue;
			}

			if (!highestByPartition.TryGetValue(topicPartition, out var highest) || offset > highest)
			{
				highestByPartition[topicPartition] = offset;
			}
		}

		if (highestByPartition.Count == 0)
		{
			return;
		}

		// One past the highest processed offset is the position to resume from.
		var positions = highestByPartition
			.Select(entry => new TopicPartitionOffset(entry.Key, new Offset(entry.Value + 1)))
			.ToList();

		_consumer.Commit(positions);
	}

	private static bool TryGetPosition(DeadLetterMessage message, out TopicPartition topicPartition, out long offset)
	{
		topicPartition = null!;
		offset = 0;

		if (message is null
			|| !message.Metadata.TryGetValue("kafka_topic", out var topic)
			|| !message.Metadata.TryGetValue("kafka_partition", out var partition)
			|| !message.Metadata.TryGetValue("kafka_offset", out var rawOffset)
			|| !int.TryParse(partition, CultureInfo.InvariantCulture, out var partitionValue)
			|| !long.TryParse(rawOffset, CultureInfo.InvariantCulture, out offset))
		{
			return false;
		}

		topicPartition = new TopicPartition(topic, new Partition(partitionValue));
		return true;
	}

	/// <summary>
	/// Peeks at messages in the specified dead letter topic without committing offsets.
	/// This is a non-destructive read intended for statistics and inspection.
	/// </summary>
	/// <param name="dlqTopic"> The dead letter topic to peek from. </param>
	/// <param name="maxMessages"> The maximum number of messages to peek. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A list of dead letter messages with metadata. </returns>
	public IReadOnlyList<DeadLetterMessage> Peek(
		string dlqTopic,
		int maxMessages,
		CancellationToken cancellationToken)
	{
		EnsureSubscribed(dlqTopic);
		var messages = new List<DeadLetterMessage>();

		while (messages.Count < maxMessages)
		{
			var result = _consumer.Consume(_dlqOptions.ConsumeTimeout);
			if (result is null || result.IsPartitionEOF)
			{
				break;
			}

			cancellationToken.ThrowIfCancellationRequested();

			var dlqMessage = ConvertToDeadLetterMessage(result);
			messages.Add(dlqMessage);
		}

		// No commit — offsets remain unchanged so messages can be re-read

		LogMessagesPeeked(_logger, messages.Count, dlqTopic);

		return messages;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_subscribedTopic is not null)
		{
			_consumer.Unsubscribe();
			_subscribedTopic = null;
		}

		_consumer.Close();
		_consumer.Dispose();

		LogConsumerStopped(_logger, _dlqOptions.ConsumerGroupId);
	}

	[LoggerMessage(KafkaEventId.DlqRetrieveFailed, LogLevel.Error,
			"Failed to retrieve dead letter messages from topic '{DlqTopic}'")]
	internal static partial void LogRetrieveFailed(ILogger logger, Exception exception, string dlqTopic);

	/// <summary>
	/// Converts a Kafka consume result into a <see cref="DeadLetterMessage"/>.
	/// </summary>
	private static DeadLetterMessage ConvertToDeadLetterMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> result)
	{
		var headers = result.Message.Headers;

		var transportMessage = new TransportMessage
		{
			Id = result.Message.Key,
			Body = result.Message.Value,
			Properties = { [TransportTelemetryConstants.PropertyKeys.PartitionKey] = result.Message.Key, },
		};

		// Extract DLQ metadata from headers
		var reason = GetHeaderValue(headers, "dlq_reason") ?? "Unknown";
		var movedAt = GetHeaderValue(headers, "dlq_moved_at");
		var attemptCount = GetHeaderValue(headers, "dlq_attempt_count");
		var originalTopic = GetHeaderValue(headers, "dlq_original_topic");

		var dlqMessage = new DeadLetterMessage
		{
			OriginalMessage = transportMessage,
			Reason = reason,
			OriginalSource = originalTopic,
			DeadLetteredAt = movedAt is not null
				? DateTimeOffset.Parse(movedAt, System.Globalization.CultureInfo.InvariantCulture)
				: result.Message.Timestamp.UtcDateTime,
			DeliveryAttempts = attemptCount is not null
				? int.Parse(attemptCount, System.Globalization.CultureInfo.InvariantCulture)
				: 0,
		};

		// Extract non-DLQ headers as metadata
		if (headers is { Count: > 0 })
		{
			foreach (var header in headers)
			{
				// A null-valued header (legal in Kafka) has nothing to record, and decoding it would throw.
				if (header.Key.StartsWith("dlq_", StringComparison.Ordinal) && header.GetValueBytes() is { } value)
				{
					dlqMessage.Metadata[header.Key] = Encoding.UTF8.GetString(value);
				}
			}
		}

		// Store Kafka-specific offset info for tracking
		dlqMessage.Metadata["kafka_topic"] = result.Topic;
		dlqMessage.Metadata["kafka_partition"] = result.Partition.Value.ToString();
		dlqMessage.Metadata["kafka_offset"] = result.Offset.Value.ToString();

		return dlqMessage;
	}

	private static string? GetHeaderValue(Headers headers, string key)
	{
		if (headers is null)
		{
			return null;
		}

		if (headers.TryGetLastBytes(key, out var bytes))
		{
			return Encoding.UTF8.GetString(bytes);
		}

		return null;
	}

	[LoggerMessage(KafkaEventId.DlqConsumerStarted, LogLevel.Information,
			"Kafka DLQ consumer started with group '{ConsumerGroupId}'")]
	private static partial void LogConsumerStarted(ILogger logger, string consumerGroupId);

	[LoggerMessage(KafkaEventId.DlqConsumerStopped, LogLevel.Information,
			"Kafka DLQ consumer stopped for group '{ConsumerGroupId}'")]
	private static partial void LogConsumerStopped(ILogger logger, string consumerGroupId);

	[LoggerMessage(KafkaEventId.DlqMessagesRetrieved, LogLevel.Information,
			"Retrieved {Count} dead letter messages from topic '{DlqTopic}'")]
	private static partial void LogMessagesRetrieved(ILogger logger, int count, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqMessagesPeeked, LogLevel.Debug,
			"Peeked {Count} dead letter messages from topic '{DlqTopic}' (non-destructive)")]
	private static partial void LogMessagesPeeked(ILogger logger, int count, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqTopicSubscribed, LogLevel.Debug,
			"Kafka DLQ consumer subscribed to topic '{DlqTopic}'")]
	private static partial void LogTopicSubscribed(ILogger logger, string dlqTopic);

	/// <summary>
	/// Ensures the consumer is subscribed to the specified topic, subscribing only
	/// when the topic changes or on first call to avoid unnecessary consumer group rebalancing.
	/// </summary>
	private void EnsureSubscribed(string dlqTopic)
	{
		if (string.Equals(_subscribedTopic, dlqTopic, StringComparison.Ordinal))
		{
			return;
		}

		_consumer.Subscribe(dlqTopic);
		_subscribedTopic = dlqTopic;
		LogTopicSubscribed(_logger, dlqTopic);
	}
}
