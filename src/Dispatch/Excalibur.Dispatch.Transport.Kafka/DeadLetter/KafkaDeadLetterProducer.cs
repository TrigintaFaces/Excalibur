// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Produces failed messages to Kafka dead letter topics with metadata headers.
/// </summary>
/// <remarks>
/// <para>
/// Messages are published to a dead letter topic named <c>{original-topic}.dead-letter</c> (configurable).
/// Original message key is preserved. DLQ metadata is stored as Kafka headers:
/// </para>
/// <list type="bullet">
///   <item><c>dlq_reason</c> - The reason for dead lettering</item>
///   <item><c>dlq_moved_at</c> - ISO 8601 timestamp of when the message was dead lettered</item>
///   <item><c>dlq_attempt_count</c> - Number of delivery attempts</item>
///   <item><c>dlq_original_topic</c> - The original source topic</item>
///   <item><c>dlq_exception_type</c> - The exception type name (if applicable)</item>
///   <item><c>dlq_exception_message</c> - The exception message (if applicable)</item>
///   <item><c>dlq_stack_trace</c> - The exception stack trace (if configured)</item>
/// </list>
/// </remarks>
internal sealed partial class KafkaDeadLetterProducer
{
	private readonly IProducer<string, byte[]> _producer;
	private readonly KafkaDeadLetterOptions _options;
	private readonly ILogger<KafkaDeadLetterProducer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaDeadLetterProducer"/> class.
	/// </summary>
	/// <param name="producer"> The Kafka producer for publishing to DLQ topics. </param>
	/// <param name="options"> The dead letter queue configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public KafkaDeadLetterProducer(
		IProducer<string, byte[]> producer,
		IOptions<KafkaDeadLetterOptions> options,
		ILogger<KafkaDeadLetterProducer> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Produces a failed message to the dead letter topic.
	/// </summary>
	/// <param name="originalMessage"> The original transport message that failed processing. </param>
	/// <param name="sourceTopic"> The topic the message was originally consumed from. </param>
	/// <param name="reason"> The reason for dead lettering. </param>
	/// <param name="deliveryAttempts"> The number of delivery attempts made. </param>
	/// <param name="exception"> Optional exception that caused the failure. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The dead letter topic, partition, and offset as a string identifier. </returns>
	public async Task<string> ProduceAsync(
		TransportMessage originalMessage,
		string sourceTopic,
		string reason,
		int deliveryAttempts,
		CancellationToken cancellationToken,
		Exception? exception = null)
	{
		var dlqTopic = _options.GetDeadLetterTopicName(sourceTopic);
		var headers = BuildHeaders(sourceTopic, reason, deliveryAttempts, exception);

		// Preserve original message key for partition affinity
		var messageKey = originalMessage.Properties.TryGetValue(
			Diagnostics.TransportTelemetryConstants.PropertyKeys.PartitionKey, out var pk)
			? pk?.ToString() ?? originalMessage.Id
			: originalMessage.Id;

		var kafkaMessage = new Message<string, byte[]>
		{
			Key = messageKey,
			Value = originalMessage.Body.ToArray(),
			Headers = headers,
			Timestamp = new Timestamp(DateTimeOffset.UtcNow),
		};

		// Copy original message properties as headers
		if (originalMessage.Properties is { Count: > 0 })
		{
			foreach (var property in originalMessage.Properties)
			{
				if (!property.Key.StartsWith("dlq_", StringComparison.Ordinal))
				{
					var value = property.Value?.ToString() ?? string.Empty;
					kafkaMessage.Headers.Add(property.Key, Encoding.UTF8.GetBytes(value));
				}
			}
		}

		var deliveryResult = await _producer
			.ProduceAsync(dlqTopic, kafkaMessage, cancellationToken)
			.ConfigureAwait(false);

		var messageId = $"{deliveryResult.Topic}:{deliveryResult.Partition.Value}:{deliveryResult.Offset.Value}";

		LogMessageMoved(_logger, originalMessage.Id, sourceTopic, dlqTopic, reason);

		return messageId;
	}

	/// <summary>
	/// Produces a message directly to the specified topic without applying the DLQ suffix.
	/// Used when reprocessing dead letter messages back to their original topic.
	/// </summary>
	/// <param name="message"> The message to produce. </param>
	/// <param name="targetTopic"> The exact target topic to produce to (no DLQ suffix applied). </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The topic, partition, and offset as a string identifier. </returns>
	public async Task<string> ProduceToOriginalTopicAsync(
		TransportMessage message,
		string targetTopic,
		CancellationToken cancellationToken)
	{
		var messageKey = message.Properties.TryGetValue(
			Diagnostics.TransportTelemetryConstants.PropertyKeys.PartitionKey, out var pk)
			? pk?.ToString() ?? message.Id
			: message.Id;

		var kafkaMessage = new Message<string, byte[]>
		{
			Key = messageKey,
			Value = message.Body.ToArray(),
			Timestamp = new Timestamp(DateTimeOffset.UtcNow),
		};

		// Copy message properties as headers (excluding DLQ metadata)
		if (message.Properties is { Count: > 0 })
		{
			kafkaMessage.Headers = new Headers();
			foreach (var property in message.Properties)
			{
				if (!property.Key.StartsWith("dlq_", StringComparison.Ordinal))
				{
					var value = property.Value?.ToString() ?? string.Empty;
					kafkaMessage.Headers.Add(property.Key, Encoding.UTF8.GetBytes(value));
				}
			}
		}

		var deliveryResult = await _producer
			.ProduceAsync(targetTopic, kafkaMessage, cancellationToken)
			.ConfigureAwait(false);

		var messageId = $"{deliveryResult.Topic}:{deliveryResult.Partition.Value}:{deliveryResult.Offset.Value}";

		LogProducedToOriginalTopic(_logger, message.Id, targetTopic);

		return messageId;
	}

	[LoggerMessage(KafkaEventId.DlqMoveFailed, LogLevel.Error,
		"Failed to move message {MessageId} to DLQ topic '{DlqTopic}'")]
	internal static partial void LogMoveFailed(ILogger logger, Exception exception, string messageId, string dlqTopic);

	[LoggerMessage(KafkaEventId.DlqMessageMoved, LogLevel.Information,
		"Moved message {MessageId} from topic '{SourceTopic}' to DLQ topic '{DlqTopic}'. Reason: {Reason}")]
	private static partial void LogMessageMoved(ILogger logger, string messageId, string sourceTopic, string dlqTopic, string reason);

	[LoggerMessage(KafkaEventId.DlqProducedToOriginalTopic, LogLevel.Information,
		"Produced reprocessed message {MessageId} to original topic '{TargetTopic}'")]
	private static partial void LogProducedToOriginalTopic(ILogger logger, string messageId, string targetTopic);

	private Headers BuildHeaders(
					string sourceTopic,
		string reason,
		int deliveryAttempts,
		Exception? exception)
	{
		var headers = new Headers
		{
			{ "dlq_reason", Encoding.UTF8.GetBytes(reason) },
			{ "dlq_moved_at", Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")) },
			{ "dlq_attempt_count", Encoding.UTF8.GetBytes(deliveryAttempts.ToString()) },
			{ "dlq_original_topic", Encoding.UTF8.GetBytes(sourceTopic) },
		};

		if (exception is not null)
		{
			headers.Add("dlq_exception_type", Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name));
			headers.Add("dlq_exception_message", Encoding.UTF8.GetBytes(exception.Message));

			if (_options.IncludeStackTrace && exception.StackTrace is not null)
			{
				headers.Add("dlq_stack_trace", Encoding.UTF8.GetBytes(exception.StackTrace));
			}
		}

		return headers;
	}
}
