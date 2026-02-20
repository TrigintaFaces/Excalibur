// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of <see cref="IDeadLetterQueueManager"/> using the native <c>$DeadLetterQueue</c> subqueue.
/// </summary>
/// <remarks>
/// <para>
/// Azure Service Bus provides a built-in dead letter subqueue per entity (queue or subscription).
/// This manager accesses it via <c>ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }</c>
/// without requiring the <c>Azure.Messaging.ServiceBus.Administration</c> package.
/// </para>
/// <para>
/// Statistics are gathered via <c>PeekMessagesAsync</c> from the DLQ subqueue, avoiding
/// the need for admin API dependencies.
/// </para>
/// </remarks>
internal sealed partial class ServiceBusDeadLetterQueueManager : IDeadLetterQueueManager, IDisposable
{
	private readonly ServiceBusClient _client;
	private readonly ServiceBusDeadLetterOptions _options;
	private readonly ILogger<ServiceBusDeadLetterQueueManager> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusDeadLetterQueueManager"/> class.
	/// </summary>
	/// <param name="client"> The Service Bus client for creating senders and receivers. </param>
	/// <param name="options"> The dead letter queue configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public ServiceBusDeadLetterQueueManager(
		ServiceBusClient client,
		IOptions<ServiceBusDeadLetterOptions> options,
		ILogger<ServiceBusDeadLetterQueueManager> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;

		LogManagerInitialized(_logger, _options.EntityPath);
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
			// Send the message body to the DLQ by publishing to the entity with
			// dead-letter properties so that it ends up in the $DeadLetterQueue subqueue.
			// Note: In real Azure SB, DeadLetterMessageAsync requires a lock token from a received message.
			// For the MoveToDeadLetterAsync interface (moving arbitrary TransportMessages), we publish
			// to the DLQ subqueue path directly.
			await using var sender = _client.CreateSender(_options.EntityPath);

			var sbMessage = new ServiceBusMessage(message.Body)
			{
				MessageId = message.Id,
				ContentType = message.ContentType,
				Subject = reason,
			};

			sbMessage.ApplicationProperties["dlq_reason"] = reason;
			sbMessage.ApplicationProperties["dlq_moved_at"] = DateTimeOffset.UtcNow.ToString("O");
			sbMessage.ApplicationProperties["dlq_original_source"] = _options.EntityPath;

			if (exception is not null)
			{
				sbMessage.ApplicationProperties["dlq_exception_type"] = exception.GetType().FullName ?? exception.GetType().Name;
				sbMessage.ApplicationProperties["dlq_exception_message"] = exception.Message;

				if (_options.IncludeStackTrace && exception.StackTrace is not null)
				{
					sbMessage.ApplicationProperties["dlq_exception_stacktrace"] = exception.StackTrace;
				}
			}

			await sender.SendMessageAsync(sbMessage, cancellationToken).ConfigureAwait(false);

			LogMessageMoved(_logger, message.Id, _options.EntityPath);

			return message.Id;
		}
		catch (ServiceBusException ex)
		{
			LogMoveFailed(_logger, ex, message.Id, _options.EntityPath);
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
			await using var receiver = _client.CreateReceiver(
				_options.EntityPath,
				new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

			var peeked = await receiver.PeekMessagesAsync(
				maxMessages, cancellationToken: cancellationToken).ConfigureAwait(false);

			var result = new List<DeadLetterMessage>(peeked.Count);

			foreach (var msg in peeked)
			{
				var cloudMessage = new TransportMessage
				{
					Id = msg.MessageId ?? Guid.NewGuid().ToString(),
					Body = msg.Body.ToArray(),
					ContentType = msg.ContentType,
				};

				var dlqMessage = new DeadLetterMessage
				{
					OriginalMessage = cloudMessage,
					Reason = msg.DeadLetterReason ?? msg.Subject ?? "Unknown",
					DeadLetteredAt = msg.EnqueuedTime,
					DeliveryAttempts = msg.DeliveryCount,
					OriginalSource = msg.ApplicationProperties.TryGetValue("dlq_original_source", out var src)
					? src?.ToString()
					: _options.EntityPath,
				};

				result.Add(dlqMessage);
			}

			LogMessagesRetrieved(_logger, result.Count, _options.EntityPath);

			return result;
		}
		catch (ServiceBusException ex)
		{
			LogRetrieveFailed(_logger, ex, _options.EntityPath);
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

		var sw = Stopwatch.StartNew();
		var result = new ReprocessResult();
		var messageList = messages as IList<DeadLetterMessage> ?? [.. messages];
		var maxMessages = options.MaxMessages ?? messageList.Count;

		// Create receiver for the DLQ subqueue to receive-and-complete
		await using var dlqReceiver = _client.CreateReceiver(
			_options.EntityPath,
			new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

		await using var sender = _client.CreateSender(
			options.TargetQueue ?? _options.EntityPath);

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

				var sbMessage = new ServiceBusMessage(messageToReprocess.Body)
				{
					MessageId = messageToReprocess.Id,
					ContentType = messageToReprocess.ContentType,
				};

				await sender.SendMessageAsync(sbMessage, cancellationToken).ConfigureAwait(false);

				result.SuccessCount++;
				LogMessageReprocessed(_logger, dlqMessage.OriginalMessage.Id, options.TargetQueue ?? _options.EntityPath);

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

		sw.Stop();
		result.ProcessingTime = sw.Elapsed;

		return result;
	}

	/// <inheritdoc/>
	public async Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		await using var receiver = _client.CreateReceiver(
			_options.EntityPath,
			new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

		var peeked = await receiver.PeekMessagesAsync(
			_options.StatisticsPeekCount, cancellationToken: cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		var stats = new DeadLetterStatistics
		{
			MessageCount = peeked.Count,
			GeneratedAt = now,
		};

		if (peeked.Count > 0)
		{
			stats.AverageDeliveryAttempts = peeked.Average(m => m.DeliveryCount);
			stats.OldestMessageAge = now - peeked.Min(m => m.EnqueuedTime);
			stats.NewestMessageAge = now - peeked.Max(m => m.EnqueuedTime);

			foreach (var msg in peeked)
			{
				var reason = msg.DeadLetterReason ?? msg.Subject ?? "Unknown";
				stats.ReasonBreakdown.TryGetValue(reason, out var reasonCount);
				stats.ReasonBreakdown[reason] = reasonCount + 1;

				var source = msg.ApplicationProperties.TryGetValue("dlq_original_source", out var src)
					? src?.ToString() ?? _options.EntityPath
					: _options.EntityPath;
				stats.SourceBreakdown.TryGetValue(source, out var sourceCount);
				stats.SourceBreakdown[source] = sourceCount + 1;

				var messageType = msg.ContentType ?? "unknown";
				stats.MessageTypeBreakdown.TryGetValue(messageType, out var typeCount);
				stats.MessageTypeBreakdown[messageType] = typeCount + 1;

				stats.SizeInBytes += msg.Body.ToArray().Length;
			}
		}

		LogStatisticsRetrieved(_logger, stats.MessageCount, _options.EntityPath);

		return stats;
	}

	/// <inheritdoc/>
	public async Task<int> PurgeDeadLetterQueueAsync(
		CancellationToken cancellationToken)
	{
		try
		{
			await using var receiver = _client.CreateReceiver(
				_options.EntityPath,
				new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

			var purgedCount = 0;

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var batch = await receiver.ReceiveMessagesAsync(
					_options.MaxBatchSize,
					_options.ReceiveWaitTime,
					cancellationToken).ConfigureAwait(false);

				if (batch.Count == 0)
				{
					break;
				}

				foreach (var msg in batch)
				{
					await receiver.CompleteMessageAsync(msg, cancellationToken).ConfigureAwait(false);
					purgedCount++;
				}
			}

			LogPurged(_logger, purgedCount, _options.EntityPath);

			return purgedCount;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogPurgeFailed(_logger, ex, _options.EntityPath);
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

	[LoggerMessage(AzureServiceBusEventId.DlqManagerInitialized, LogLevel.Information,
		"Azure Service Bus DLQ manager initialized for entity '{EntityPath}'")]
	private static partial void LogManagerInitialized(ILogger logger, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqMessageMoved, LogLevel.Information,
		"Moved message {MessageId} to DLQ for entity '{EntityPath}'")]
	private static partial void LogMessageMoved(ILogger logger, string messageId, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqMoveFailed, LogLevel.Error,
		"Failed to move message {MessageId} to DLQ for entity '{EntityPath}'")]
	private static partial void LogMoveFailed(ILogger logger, Exception exception, string messageId, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqMessagesRetrieved, LogLevel.Debug,
		"Retrieved {Count} messages from DLQ for entity '{EntityPath}'")]
	private static partial void LogMessagesRetrieved(ILogger logger, int count, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqRetrieveFailed, LogLevel.Error,
		"Failed to retrieve messages from DLQ for entity '{EntityPath}'")]
	private static partial void LogRetrieveFailed(ILogger logger, Exception exception, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqMessageReprocessed, LogLevel.Information,
		"Reprocessed DLQ message {MessageId} to entity '{TargetEntity}'")]
	private static partial void LogMessageReprocessed(ILogger logger, string messageId, string targetEntity);

	[LoggerMessage(AzureServiceBusEventId.DlqReprocessFailed, LogLevel.Error,
		"Failed to reprocess DLQ message {MessageId}")]
	private static partial void LogReprocessFailed(ILogger logger, Exception exception, string messageId);

	[LoggerMessage(AzureServiceBusEventId.DlqStatisticsRetrieved, LogLevel.Debug,
		"Retrieved DLQ statistics: {MessageCount} messages for entity '{EntityPath}'")]
	private static partial void LogStatisticsRetrieved(ILogger logger, int messageCount, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqPurged, LogLevel.Warning,
		"Purged {Count} messages from DLQ for entity '{EntityPath}'")]
	private static partial void LogPurged(ILogger logger, int count, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqPurgeFailed, LogLevel.Error,
		"Failed to purge DLQ for entity '{EntityPath}'")]
	private static partial void LogPurgeFailed(ILogger logger, Exception exception, string entityPath);

	[LoggerMessage(AzureServiceBusEventId.DlqMessageSkipped, LogLevel.Debug,
		"Skipped DLQ message {MessageId}: {SkipReason}")]
	private static partial void LogMessageSkipped(ILogger logger, string messageId, string skipReason);
}
