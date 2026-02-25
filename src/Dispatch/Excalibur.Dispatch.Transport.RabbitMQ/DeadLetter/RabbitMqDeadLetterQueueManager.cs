// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="IDeadLetterQueueManager"/> using dead letter exchanges (DLX).
/// </summary>
/// <remarks>
/// <para>
/// Dead letter messages are routed through a dedicated DLX exchange and stored in a configured DLQ queue.
/// Message metadata (reason, timestamp, original exchange/routing key) is stored as RabbitMQ headers.
/// </para>
/// <para>
/// Peek semantics are achieved via <c>BasicGetAsync(autoAck: false)</c> followed by
/// <c>BasicNackAsync(requeue: true)</c> to avoid consuming the message.
/// </para>
/// </remarks>
internal sealed partial class RabbitMqDeadLetterQueueManager : IDeadLetterQueueManager, IDisposable
{
	private readonly IChannel _channel;
	private readonly RabbitMqDeadLetterOptions _options;
	private readonly ILogger<RabbitMqDeadLetterQueueManager> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqDeadLetterQueueManager"/> class.
	/// </summary>
	/// <param name="channel"> The RabbitMQ channel for DLQ operations. </param>
	/// <param name="options"> The dead letter queue configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public RabbitMqDeadLetterQueueManager(
		IChannel channel,
		IOptions<RabbitMqDeadLetterOptions> options,
		ILogger<RabbitMqDeadLetterQueueManager> logger)
	{
		ArgumentNullException.ThrowIfNull(channel);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_channel = channel;
		_options = options.Value;
		_logger = logger;

		LogManagerInitialized(_logger, _options.QueueName);
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

		try
		{
			var properties = new RabbitMqBasicProperties
			{
				MessageId = message.Id,
				ContentType = message.ContentType,
				Headers = new Dictionary<string, object?>
				{
					["dlq_reason"] = reason,
					["dlq_moved_at"] = DateTimeOffset.UtcNow.ToString("O"),
					["dlq_original_source"] = _options.QueueName,
				},
			};

			if (exception is not null)
			{
				properties.Headers["dlq_exception_type"] = exception.GetType().FullName ?? exception.GetType().Name;
				properties.Headers["dlq_exception_message"] = exception.Message;

				if (_options.IncludeStackTrace && exception.StackTrace is not null)
				{
					properties.Headers["dlq_exception_stacktrace"] = exception.StackTrace;
				}
			}

			await _channel.BasicPublishAsync(
				exchange: _options.Exchange,
				routingKey: _options.RoutingKey,
				mandatory: false,
				basicProperties: properties,
				body: message.Body,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			LogMessageMoved(_logger, message.Id, _options.QueueName);

			return message.Id;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogMoveFailed(_logger, ex, message.Id, _options.QueueName);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = new List<DeadLetterMessage>();

			for (var i = 0; i < maxMessages; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var getResult = await _channel.BasicGetAsync(
					_options.QueueName, autoAck: false, cancellationToken).ConfigureAwait(false);

				if (getResult is null)
				{
					break;
				}

				var cloudMessage = new TransportMessage
				{
					Id = getResult.BasicProperties.MessageId ?? Guid.NewGuid().ToString(),
					Body = getResult.Body.ToArray(),
					ContentType = getResult.BasicProperties.ContentType,
				};

				var dlqMessage = new DeadLetterMessage
				{
					OriginalMessage = cloudMessage,
					Reason = GetHeaderString(getResult.BasicProperties.Headers, "dlq_reason") ?? "Unknown",
					DeadLetteredAt = ParseHeaderDateTimeOffset(getResult.BasicProperties.Headers, "dlq_moved_at"),
					DeliveryAttempts = (int)getResult.DeliveryTag,
					OriginalSource = GetHeaderString(getResult.BasicProperties.Headers, "dlq_original_source"),
				};

				result.Add(dlqMessage);

				// Nack with requeue for peek semantics (non-destructive)
				await _channel.BasicNackAsync(
					getResult.DeliveryTag, multiple: false, requeue: true, cancellationToken).ConfigureAwait(false);
			}

			LogMessagesRetrieved(_logger, result.Count, _options.QueueName);

			return result;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogRetrieveFailed(_logger, ex, _options.QueueName);
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

			if (options.MessageFilter is not null && !options.MessageFilter(dlqMessage))
			{
				result.SkippedCount++;
				LogMessageSkipped(_logger, dlqMessage.OriginalMessage.Id, "Filter excluded");
				continue;
			}

			try
			{
				var messageToReprocess = dlqMessage.OriginalMessage;

				if (options.MessageTransform is not null)
				{
					messageToReprocess = options.MessageTransform(messageToReprocess);
				}

				// Determine target: use TargetQueue override, original source from x-death headers, or default exchange
				var targetExchange = options.TargetQueue
					?? dlqMessage.OriginalSource
					?? string.Empty;

				var properties = new RabbitMqBasicProperties
				{
					MessageId = messageToReprocess.Id,
					ContentType = messageToReprocess.ContentType,
				};

				await _channel.BasicPublishAsync(
					exchange: targetExchange,
					routingKey: _options.RoutingKey,
					mandatory: false,
					basicProperties: properties,
					body: messageToReprocess.Body,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				result.SuccessCount++;
				LogMessageReprocessed(_logger, dlqMessage.OriginalMessage.Id, targetExchange);

				if (options.RetryDelay > TimeSpan.Zero)
				{
					await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				result.FailureCount++;
				result.Failures.Add(new ReprocessFailure
				{
					Message = dlqMessage,
					Reason = ex.Message,
					Exception = ex,
				});

				LogReprocessFailed(_logger, ex, dlqMessage.OriginalMessage.Id);
			}
		}

		result.ProcessingTime = sw.GetElapsedTime();

		return result;
	}

	/// <inheritdoc/>
	public async Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		// Use QueueDeclarePassiveAsync to get the message count without modifying the queue
		var queueDeclareOk = await _channel.QueueDeclarePassiveAsync(
			_options.QueueName, cancellationToken).ConfigureAwait(false);

		var messageCount = (int)queueDeclareOk.MessageCount;
		var now = DateTimeOffset.UtcNow;

		var stats = new DeadLetterStatistics
		{
			MessageCount = messageCount,
			GeneratedAt = now,
		};

		// Peek messages for detailed statistics if there are any
		if (messageCount > 0)
		{
			var peekCount = Math.Min(messageCount, _options.MaxBatchSize);

			for (var i = 0; i < peekCount; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var getResult = await _channel.BasicGetAsync(
					_options.QueueName, autoAck: false, cancellationToken).ConfigureAwait(false);

				if (getResult is null)
				{
					break;
				}

				var reason = GetHeaderString(getResult.BasicProperties.Headers, "dlq_reason") ?? "Unknown";
				var source = GetHeaderString(getResult.BasicProperties.Headers, "dlq_original_source") ?? _options.QueueName;
				var movedAt = ParseHeaderDateTimeOffset(getResult.BasicProperties.Headers, "dlq_moved_at");
				var contentType = getResult.BasicProperties.ContentType ?? "unknown";

				stats.ReasonBreakdown.TryGetValue(reason, out var reasonCount);
				stats.ReasonBreakdown[reason] = reasonCount + 1;

				stats.SourceBreakdown.TryGetValue(source, out var sourceCount);
				stats.SourceBreakdown[source] = sourceCount + 1;

				stats.MessageTypeBreakdown.TryGetValue(contentType, out var typeCount);
				stats.MessageTypeBreakdown[contentType] = typeCount + 1;

				stats.SizeInBytes += getResult.Body.Length;

				stats.AverageDeliveryAttempts = ((stats.AverageDeliveryAttempts * i) + 1) / (i + 1);

				if (stats.OldestMessageAge == default || (now - movedAt) > stats.OldestMessageAge)
				{
					stats.OldestMessageAge = now - movedAt;
				}

				if (stats.NewestMessageAge == default || (now - movedAt) < stats.NewestMessageAge)
				{
					stats.NewestMessageAge = now - movedAt;
				}

				// Nack with requeue for non-destructive peek
				await _channel.BasicNackAsync(
					getResult.DeliveryTag, multiple: false, requeue: true, cancellationToken).ConfigureAwait(false);
			}
		}

		LogStatisticsRetrieved(_logger, stats.MessageCount, _options.QueueName);

		return stats;
	}

	/// <inheritdoc/>
	public async Task<int> PurgeDeadLetterQueueAsync(
		CancellationToken cancellationToken)
	{
		try
		{
			var purgeResult = await _channel.QueuePurgeAsync(
				_options.QueueName, cancellationToken).ConfigureAwait(false);

			var purgedCount = (int)purgeResult;

			LogPurged(_logger, purgedCount, _options.QueueName);

			return purgedCount;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogPurgeFailed(_logger, ex, _options.QueueName);
			throw;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	private static string? GetHeaderString(IDictionary<string, object?>? headers, string key)
	{
		if (headers is null || !headers.TryGetValue(key, out var value) || value is null)
		{
			return null;
		}

		return value switch
		{
			byte[] bytes => Encoding.UTF8.GetString(bytes),
			string s => s,
			_ => value.ToString(),
		};
	}

	private static DateTimeOffset ParseHeaderDateTimeOffset(IDictionary<string, object?>? headers, string key)
	{
		var str = GetHeaderString(headers, key);
		return str is not null && DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
			? result
			: DateTimeOffset.UtcNow;
	}

	[LoggerMessage(RabbitMqEventId.DlqManagerInitialized, LogLevel.Information,
		"RabbitMQ DLQ manager initialized for queue '{QueueName}'")]
	private static partial void LogManagerInitialized(ILogger logger, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqMessageMoved, LogLevel.Information,
		"Moved message {MessageId} to DLQ '{QueueName}'")]
	private static partial void LogMessageMoved(ILogger logger, string messageId, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqMoveFailed, LogLevel.Error,
		"Failed to move message {MessageId} to DLQ '{QueueName}'")]
	private static partial void LogMoveFailed(ILogger logger, Exception exception, string messageId, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqMessagesRetrieved, LogLevel.Debug,
		"Retrieved {Count} messages from DLQ '{QueueName}'")]
	private static partial void LogMessagesRetrieved(ILogger logger, int count, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqRetrieveFailed, LogLevel.Error,
		"Failed to retrieve messages from DLQ '{QueueName}'")]
	private static partial void LogRetrieveFailed(ILogger logger, Exception exception, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqMessageReprocessed, LogLevel.Information,
		"Reprocessed DLQ message {MessageId} to exchange '{TargetExchange}'")]
	private static partial void LogMessageReprocessed(ILogger logger, string messageId, string targetExchange);

	[LoggerMessage(RabbitMqEventId.DlqReprocessFailed, LogLevel.Error,
		"Failed to reprocess DLQ message {MessageId}")]
	private static partial void LogReprocessFailed(ILogger logger, Exception exception, string messageId);

	[LoggerMessage(RabbitMqEventId.DlqStatisticsRetrieved, LogLevel.Debug,
		"Retrieved DLQ statistics: {MessageCount} messages in queue '{QueueName}'")]
	private static partial void LogStatisticsRetrieved(ILogger logger, int messageCount, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqPurged, LogLevel.Warning,
		"Purged {Count} messages from DLQ '{QueueName}'")]
	private static partial void LogPurged(ILogger logger, int count, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqPurgeFailed, LogLevel.Error,
		"Failed to purge DLQ '{QueueName}'")]
	private static partial void LogPurgeFailed(ILogger logger, Exception exception, string queueName);

	[LoggerMessage(RabbitMqEventId.DlqMessageSkipped, LogLevel.Debug,
		"Skipped DLQ message {MessageId}: {SkipReason}")]
	private static partial void LogMessageSkipped(ILogger logger, string messageId, string skipReason);
}
