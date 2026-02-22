// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="IDeadLetterQueueManager"/> using topic-based dead letter queues.
/// </summary>
/// <remarks>
/// <para>
/// Dead letter messages are stored in dedicated Kafka topics following the naming convention
/// <c>{original-topic}.dead-letter</c>. A separate consumer group is used for DLQ processing
/// to avoid interfering with the main application consumer group.
/// </para>
/// <para>
/// Message metadata (reason, timestamp, attempt count, original topic) is stored as Kafka headers
/// for efficient retrieval without deserializing the message body.
/// </para>
/// </remarks>
internal sealed partial class KafkaDeadLetterQueueManager : IDeadLetterQueueManager, IDisposable
{
	private readonly KafkaDeadLetterProducer _producer;
	private readonly KafkaDeadLetterConsumer _consumer;
	private readonly KafkaDeadLetterOptions _options;
	private readonly KafkaOptions _kafkaOptions;
	private readonly ILogger<KafkaDeadLetterQueueManager> _logger;
	private readonly string _defaultSourceTopic;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaDeadLetterQueueManager"/> class.
	/// </summary>
	/// <param name="producer"> The DLQ producer for publishing failed messages. </param>
	/// <param name="consumer"> The DLQ consumer for reading dead letter messages. </param>
	/// <param name="dlqOptions"> The dead letter queue configuration options. </param>
	/// <param name="kafkaOptions"> The Kafka connection options. </param>
	/// <param name="logger"> The logger instance. </param>
	public KafkaDeadLetterQueueManager(
		KafkaDeadLetterProducer producer,
		KafkaDeadLetterConsumer consumer,
		IOptions<KafkaDeadLetterOptions> dlqOptions,
		IOptions<KafkaOptions> kafkaOptions,
		ILogger<KafkaDeadLetterQueueManager> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		_options = dlqOptions?.Value ?? throw new ArgumentNullException(nameof(dlqOptions));
		_kafkaOptions = kafkaOptions?.Value ?? throw new ArgumentNullException(nameof(kafkaOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_defaultSourceTopic = !string.IsNullOrWhiteSpace(_kafkaOptions.Topic)
			? _kafkaOptions.Topic
			: "default";

		LogManagerInitialized(_logger, _defaultSourceTopic);
	}

	/// <inheritdoc/>
	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		var sourceTopic = message.Properties.TryGetValue(
			Diagnostics.TransportTelemetryConstants.Tags.Source, out var src)
			? src?.ToString() ?? _defaultSourceTopic
			: _defaultSourceTopic;

		try
		{
			return await _producer.ProduceAsync(
				message,
				sourceTopic,
				reason,
				deliveryAttempts: 0,
				cancellationToken,
				exception).ConfigureAwait(false);
		}
		catch (ProduceException<string, byte[]> ex)
		{
			KafkaDeadLetterProducer.LogMoveFailed(
				_logger, ex, message.Id, _options.GetDeadLetterTopicName(sourceTopic));
			throw;
		}
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		var dlqTopic = _options.GetDeadLetterTopicName(_defaultSourceTopic);

		try
		{
			var messages = _consumer.Consume(dlqTopic, maxMessages, cancellationToken);
			return Task.FromResult(messages);
		}
		catch (ConsumeException ex)
		{
			KafkaDeadLetterConsumer.LogRetrieveFailed(_logger, ex, dlqTopic);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ArgumentNullException.ThrowIfNull(options);

		var sw = ValueStopwatch.StartNew();
		var result = new ReprocessResult();
		var messageList = messages as IList<DeadLetterMessage> ?? [.. messages];
		var maxMessages = options.MaxMessages ?? messageList.Count;

		foreach (var dlqMessage in messageList.Take(maxMessages))
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Apply filter
			if (options.MessageFilter is not null && !options.MessageFilter(dlqMessage))
			{
				result.SkippedCount++;
				LogMessageSkipped(_logger, dlqMessage.OriginalMessage.Id, "Filter excluded");
				continue;
			}

			try
			{
				var messageToReprocess = dlqMessage.OriginalMessage;

				// Apply transformation
				if (options.MessageTransform is not null)
				{
					messageToReprocess = options.MessageTransform(messageToReprocess);
				}

				// Determine target topic
				var targetTopic = options.TargetQueue
								  ?? dlqMessage.OriginalSource
								  ?? _defaultSourceTopic;

				// Produce back to the original/target topic (bypasses DLQ suffix)
				await _producer.ProduceToOriginalTopicAsync(
					messageToReprocess,
					targetTopic,
					cancellationToken).ConfigureAwait(false);

				result.SuccessCount++;
				LogMessageReprocessed(_logger, dlqMessage.OriginalMessage.Id, targetTopic);

				if (options.RetryDelay > TimeSpan.Zero)
				{
					await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				result.FailureCount++;
				result.Failures.Add(new ReprocessFailure { Message = dlqMessage, Reason = ex.Message, Exception = ex, });

				LogReprocessFailed(_logger, ex, dlqMessage.OriginalMessage.Id);
			}
		}

		result.ProcessingTime = sw.Elapsed;

		return result;
	}

	/// <inheritdoc/>
	public Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		var dlqTopic = _options.GetDeadLetterTopicName(_defaultSourceTopic);

		// Peek at messages non-destructively (no offset commit) for statistics
		var messages = _consumer.Peek(dlqTopic, maxMessages: 1000, cancellationToken);
		var now = DateTimeOffset.UtcNow;

		var stats = new DeadLetterStatistics { MessageCount = messages.Count, GeneratedAt = now, };

		if (messages.Count > 0)
		{
			stats.AverageDeliveryAttempts = messages.Average(m => m.DeliveryAttempts);
			stats.OldestMessageAge = now - messages.Min(m => m.DeadLetteredAt);
			stats.NewestMessageAge = now - messages.Max(m => m.DeadLetteredAt);

			foreach (var msg in messages)
			{
				// Reason breakdown
				if (!string.IsNullOrEmpty(msg.Reason))
				{
					stats.ReasonBreakdown.TryGetValue(msg.Reason, out var reasonCount);
					stats.ReasonBreakdown[msg.Reason] = reasonCount + 1;
				}

				// Source breakdown
				if (!string.IsNullOrEmpty(msg.OriginalSource))
				{
					stats.SourceBreakdown.TryGetValue(msg.OriginalSource, out var sourceCount);
					stats.SourceBreakdown[msg.OriginalSource] = sourceCount + 1;
				}

				// Message type breakdown
				var messageType = msg.OriginalMessage.ContentType ?? "unknown";
				stats.MessageTypeBreakdown.TryGetValue(messageType, out var typeCount);
				stats.MessageTypeBreakdown[messageType] = typeCount + 1;

				stats.SizeInBytes += msg.OriginalMessage.Body.Length;
			}
		}

		LogStatisticsRetrieved(_logger, stats.MessageCount, dlqTopic);

		return Task.FromResult(stats);
	}

	/// <inheritdoc/>
	public Task<int> PurgeDeadLetterQueueAsync(
		CancellationToken cancellationToken)
	{
		var dlqTopic = _options.GetDeadLetterTopicName(_defaultSourceTopic);

		try
		{
			// Consume all available messages to effectively purge
			var purgedCount = 0;
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var batch = _consumer.Consume(dlqTopic, maxMessages: 500, cancellationToken);
				if (batch.Count == 0)
				{
					break;
				}

				purgedCount += batch.Count;
			}

			LogPurged(_logger, purgedCount, dlqTopic);

			return Task.FromResult(purgedCount);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogPurgeFailed(_logger, ex, dlqTopic);
			throw;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_consumer.Dispose();
	}

	[LoggerMessage(KafkaEventId.DlqManagerInitialized, LogLevel.Information,
		"Kafka DLQ manager initialized for source topic '{SourceTopic}'")]
	private static partial void LogManagerInitialized(ILogger logger, string sourceTopic);

	[LoggerMessage(KafkaEventId.DlqMessageReprocessed, LogLevel.Information,
		"Reprocessed DLQ message {MessageId} to topic '{TargetTopic}'")]
	private static partial void LogMessageReprocessed(ILogger logger, string messageId, string targetTopic);

	[LoggerMessage(KafkaEventId.DlqReprocessFailed, LogLevel.Error,
		"Failed to reprocess DLQ message {MessageId}")]
	private static partial void LogReprocessFailed(ILogger logger, Exception exception, string messageId);

	[LoggerMessage(KafkaEventId.DlqStatisticsRetrieved, LogLevel.Debug,
		"Retrieved DLQ statistics: {MessageCount} messages in topic '{DlqTopic}'")]
	private static partial void LogStatisticsRetrieved(ILogger logger, int messageCount, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqPurged, LogLevel.Warning,
		"Purged {Count} messages from DLQ topic '{DlqTopic}'")]
	private static partial void LogPurged(ILogger logger, int count, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqPurgeFailed, LogLevel.Error,
		"Failed to purge DLQ topic '{DlqTopic}'")]
	private static partial void LogPurgeFailed(ILogger logger, Exception exception, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqMessageSkipped, LogLevel.Debug,
		"Skipped DLQ message {MessageId}: {SkipReason}")]
	private static partial void LogMessageSkipped(ILogger logger, string messageId, string skipReason);
}
