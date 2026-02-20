// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Globalization;
using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Processes messages from SQS dead letter queues using real AWS SQS SDK operations.
/// Implements both the SQS-specific <see cref="IDlqManager"/> and the transport-agnostic
/// <see cref="IDeadLetterQueueManager"/> interfaces.
/// </summary>
/// <param name="sqsClient"> The AWS SQS client. </param>
/// <param name="logger"> The logger. </param>
/// <param name="options"> The processor options. </param>
/// <param name="dlqOptions"> The DLQ configuration options. </param>
/// <param name="retryStrategy"> The retry strategy. </param>
/// <param name="errorTracker"> The error tracker. </param>
public sealed partial class DlqProcessor(
	IAmazonSQS sqsClient,
	ILogger<DlqProcessor> logger,
	IOptions<DlqProcessorOptions> options,
	IOptions<DlqOptions> dlqOptions,
	IRetryStrategy retryStrategy,
	IErrorTracker errorTracker) : IDlqManager, IDeadLetterQueueManager, IDisposable
{
	private const string DlqOriginalQueueUrlAttribute = "dlq_original_queue_url";
	private const string DlqMovedAtAttribute = "dlq_moved_at";
	private const string DlqReasonAttribute = "dlq_reason";
	private const string DlqAttemptCountAttribute = "dlq_attempt_count";

#pragma warning disable CA2213 // IAmazonSQS lifetime is owned by the DI container, not this class
	private readonly IAmazonSQS _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
#pragma warning restore CA2213
	private readonly ILogger<DlqProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly DlqProcessorOptions _processorOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly DlqOptions _dlqOptions = dlqOptions?.Value ?? throw new ArgumentNullException(nameof(dlqOptions));
	private volatile bool _disposed;

	#region IDlqManager Implementation

	/// <inheritdoc />
	public async Task<DlqProcessingResult> ProcessMessageAsync(
		DlqMessage message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var stopwatch = Stopwatch.StartNew();
		try
		{
			LogProcessingMessage(_logger, message.MessageId);

			var attempt = message.AttemptCount + 1;
			if (!retryStrategy.ShouldRetry(attempt, message.LastError != null
					? new InvalidOperationException(message.LastError)
					: null))
			{
				LogMessageExhaustedRetries(_logger, message.MessageId, attempt);
				return new DlqProcessingResult
				{
					Success = false,
					MessageId = message.MessageId,
					Action = DlqAction.Skipped,
					ProcessedAt = DateTimeOffset.UtcNow,
					RetryAttempts = attempt,
				};
			}

			// Delete from DLQ after processing
			if (message.ReceiptHandle != null && _dlqOptions.DeadLetterQueueUrl != null)
			{
				await _sqsClient
					.DeleteMessageAsync(
						new DeleteMessageRequest
						{
							QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
							ReceiptHandle = message.ReceiptHandle,
						}, cancellationToken).ConfigureAwait(false);
			}

			stopwatch.Stop();
			LogMessageProcessed(_logger, message.MessageId, stopwatch.ElapsedMilliseconds);

			return new DlqProcessingResult
			{
				Success = true,
				MessageId = message.MessageId,
				Action = DlqAction.Redriven,
				ProcessedAt = DateTimeOffset.UtcNow,
				RetryAttempts = attempt,
			};
		}
		catch (Exception ex)
		{
			LogProcessingFailed(_logger, message.MessageId, ex);

			await errorTracker.RecordErrorAsync(
				message.MessageId,
				new ErrorDetail
				{
					Timestamp = DateTimeOffset.UtcNow,
					Message = ex.Message,
					ErrorType = ex.GetType().FullName,
					StackTrace = ex.StackTrace,
				},
				cancellationToken).ConfigureAwait(false);

			return new DlqProcessingResult
			{
				Success = false,
				MessageId = message.MessageId,
				Action = DlqAction.RetryFailed,
				ErrorMessage = ex.Message,
				ProcessedAt = DateTimeOffset.UtcNow,
			};
		}
	}

	/// <inheritdoc />
	public async Task<bool> MoveToDeadLetterQueueAsync(
		DlqMessage message,
		string reason,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			LogNoDlqConfigured(_logger);
			return false;
		}

		LogMovingToDlq(_logger, message.MessageId, reason);

		var messageAttributes = new Dictionary<string, MessageAttributeValue>
		{
			[DlqReasonAttribute] = new() { DataType = "String", StringValue = reason, },
			[DlqMovedAtAttribute] = new() { DataType = "String", StringValue = DateTimeOffset.UtcNow.ToString("O"), },
			[DlqAttemptCountAttribute] = new() { DataType = "Number", StringValue = message.AttemptCount.ToString(), },
		};

		if (message.SourceQueueUrl != null)
		{
			messageAttributes[DlqOriginalQueueUrlAttribute] = new MessageAttributeValue
			{
				DataType = "String",
				StringValue = message.SourceQueueUrl.ToString(),
			};
		}

		// Copy existing message attributes
		foreach (var attr in message.Attributes)
		{
			if (!messageAttributes.ContainsKey(attr.Key))
			{
				messageAttributes[attr.Key] = new MessageAttributeValue { DataType = "String", StringValue = attr.Value, };
			}
		}

		var sendRequest = new SendMessageRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			MessageBody = message.Body,
			MessageAttributes = messageAttributes,
		};

		await _sqsClient.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);

		LogMovedToDlq(_logger, message.MessageId);
		return true;
	}

	/// <inheritdoc />
	public async Task<int> RedriveMessagesAsync(
		CancellationToken cancellationToken,
		IEnumerable<string>? messageIds = null,
		int maxMessages = 10)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			LogNoDlqConfigured(_logger);
			return 0;
		}

		LogRedrivingMessages(_logger, maxMessages);

		var receiveRequest = new ReceiveMessageRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			MaxNumberOfMessages = Math.Min(maxMessages, 10), // SQS max batch
			MessageSystemAttributeNames = ["All"],
			MessageAttributeNames = ["All"],
		};

		var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken)
			.ConfigureAwait(false);

		if (response.Messages.Count == 0)
		{
			return 0;
		}

		var messageIdSet = messageIds != null ? new HashSet<string>(messageIds) : null;
		var redrivenCount = 0;

		foreach (var sqsMessage in response.Messages)
		{
			// Filter by message IDs if provided
			if (messageIdSet != null && !messageIdSet.Contains(sqsMessage.MessageId))
			{
				continue;
			}

			// Determine the source queue URL from message attributes
			var sourceQueueUrl = GetSourceQueueUrl(sqsMessage);
			if (sourceQueueUrl is null)
			{
				LogNoSourceQueueUrl(_logger, sqsMessage.MessageId);
				continue;
			}

			// Preserve all message attributes when redriving
			var sendRequest = new SendMessageRequest
			{
				QueueUrl = sourceQueueUrl,
				MessageBody = sqsMessage.Body,
				MessageAttributes = sqsMessage.MessageAttributes,
			};

			await _sqsClient.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);

			// Delete from DLQ after successful redrive
			await _sqsClient
				.DeleteMessageAsync(
					new DeleteMessageRequest
					{
						QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
						ReceiptHandle = sqsMessage.ReceiptHandle,
					}, cancellationToken).ConfigureAwait(false);

			redrivenCount++;
		}

		LogRedrivenMessages(_logger, redrivenCount);
		return redrivenCount;
	}

	/// <inheritdoc />
	public async Task<DlqStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			return new DlqStatistics { GeneratedAt = DateTimeOffset.UtcNow };
		}

		var attributeRequest = new GetQueueAttributesRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			AttributeNames =
			[
				"ApproximateNumberOfMessages",
				"ApproximateNumberOfMessagesNotVisible",
				"CreatedTimestamp",
				"LastModifiedTimestamp",
			],
		};

		var attributeResponse = await _sqsClient.GetQueueAttributesAsync(
			attributeRequest, cancellationToken).ConfigureAwait(false);

		var totalMessages = 0;
		if (attributeResponse.Attributes.TryGetValue("ApproximateNumberOfMessages", out var msgCount))
		{
			_ = int.TryParse(msgCount, out totalMessages);
		}

		var notVisible = 0;
		if (attributeResponse.Attributes.TryGetValue("ApproximateNumberOfMessagesNotVisible", out var nvCount))
		{
			_ = int.TryParse(nvCount, out notVisible);
		}

		return new DlqStatistics
		{
			TotalMessages = totalMessages + notVisible,
			MessagesProcessed = 0, // Would need external state tracking
			MessagesRequeued = 0,
			MessagesDiscarded = 0,
			GeneratedAt = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Processes a batch of messages from the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The number of messages processed. </returns>
	public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			LogNoDlqConfigured(_logger);
			return 0;
		}

		LogProcessingBatch(_logger, _processorOptions.BatchSize);

		var receiveRequest = new ReceiveMessageRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			MaxNumberOfMessages = Math.Min(_processorOptions.BatchSize, 10),
			WaitTimeSeconds = 5,
			MessageSystemAttributeNames = ["All"],
			MessageAttributeNames = ["All"],
		};

		var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken)
			.ConfigureAwait(false);

		var processedCount = 0;
		foreach (var sqsMessage in response.Messages)
		{
			var dlqMessage = ConvertToDlqMessage(sqsMessage);
			var result = await ProcessMessageAsync(dlqMessage, cancellationToken).ConfigureAwait(false);
			if (result.Success)
			{
				processedCount++;
			}
		}

		LogBatchProcessed(_logger, processedCount, response.Messages.Count);
		return processedCount;
	}

	/// <summary>
	/// Purges all messages from the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Estimated number of messages purged. </returns>
	public async Task<int> PurgeMessagesAsync(CancellationToken cancellationToken)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			LogNoDlqConfigured(_logger);
			return 0;
		}

		// Get approximate message count before purge
		var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
		var estimatedCount = stats.TotalMessages;

		LogPurgingDlq(_logger);

		await _sqsClient.PurgeQueueAsync(new PurgeQueueRequest { QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(), }, cancellationToken)
			.ConfigureAwait(false);

		LogPurgedDlq(_logger, estimatedCount);
		return estimatedCount;
	}

	/// <summary>
	/// Requeues a single message from the DLQ back to its source queue.
	/// </summary>
	/// <param name="messageId"> The message ID to requeue. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> True if the message was successfully requeued. </returns>
	public async Task<bool> RequeueMessageAsync(
		string messageId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(messageId);

		var redriven = await RedriveMessagesAsync(cancellationToken, [messageId], 10).ConfigureAwait(false);
		return redriven > 0;
	}

	#endregion

	#region IDeadLetterQueueManager Implementation

	/// <inheritdoc />
	async Task<string> IDeadLetterQueueManager.MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var dlqMessage = new DlqMessage
		{
			MessageId = message.Id,
			Body = Encoding.UTF8.GetString(message.Body.Span),
			SourceQueueUrl = message.Properties.TryGetValue("dispatch.source", out var sourceVal) && sourceVal is string sourceStr
				? new Uri(sourceStr)
				: null,
			AttemptCount = 0,
			FirstSentTimestamp = message.CreatedAt.UtcDateTime,
			LastError = exception?.Message,
			DlqReason = reason,
		};

		await MoveToDeadLetterQueueAsync(dlqMessage, reason, cancellationToken).ConfigureAwait(false);
		return message.Id;
	}

	/// <inheritdoc />
	async Task<IReadOnlyList<DeadLetterMessage>> IDeadLetterQueueManager.GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			return [];
		}

		var receiveRequest = new ReceiveMessageRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			MaxNumberOfMessages = Math.Min(maxMessages, 10),
			VisibilityTimeout = 0, // Peek without consuming (messages immediately visible again)
			MessageSystemAttributeNames = ["All"],
			MessageAttributeNames = ["All"],
		};

		var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken)
			.ConfigureAwait(false);

		var result = new List<DeadLetterMessage>(response.Messages.Count);
		foreach (var sqsMessage in response.Messages)
		{
			result.Add(ConvertToDeadLetterMessage(sqsMessage));
		}

		return result;
	}

	/// <inheritdoc />
	async Task<ReprocessResult> IDeadLetterQueueManager.ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions reprocessOptions,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ArgumentNullException.ThrowIfNull(reprocessOptions);

		var stopwatch = Stopwatch.StartNew();
		var result = new ReprocessResult();
		var messageList = messages.ToList();

		foreach (var dlqMsg in messageList)
		{
			if (reprocessOptions.MessageFilter != null && !reprocessOptions.MessageFilter(dlqMsg))
			{
				result.SkippedCount++;
				continue;
			}

			try
			{
				var targetQueue = reprocessOptions.TargetQueue
								  ?? dlqMsg.OriginalSource
								  ?? throw new InvalidOperationException("No target queue available for reprocessing.");

				var cloudMessage = dlqMsg.OriginalMessage;
				if (reprocessOptions.MessageTransform != null)
				{
					cloudMessage = reprocessOptions.MessageTransform(cloudMessage);
				}

				var sendRequest = new SendMessageRequest
				{
					QueueUrl = targetQueue,
					MessageBody = Encoding.UTF8.GetString(cloudMessage.Body.Span),
				};

				await _sqsClient.SendMessageAsync(sendRequest, cancellationToken).ConfigureAwait(false);

				if (reprocessOptions.RemoveFromDlq && _dlqOptions.DeadLetterQueueUrl != null
												   && cloudMessage.Properties.TryGetValue("dispatch.lock.token", out var lockTokenVal)
												   && lockTokenVal is string lockToken)
				{
					await _sqsClient
						.DeleteMessageAsync(
							new DeleteMessageRequest { QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(), ReceiptHandle = lockToken, },
							cancellationToken).ConfigureAwait(false);
				}

				result.SuccessCount++;
			}
			catch (Exception ex)
			{
				result.FailureCount++;
				result.Failures.Add(new ReprocessFailure
				{
					Message = dlqMsg,
					Reason = ex.Message,
					Exception = ex,
					FailedAt = DateTimeOffset.UtcNow,
				});
			}
		}

		result.ProcessingTime = stopwatch.Elapsed;
		return result;
	}

	/// <inheritdoc />
	async Task<DeadLetterStatistics> IDeadLetterQueueManager.GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		if (_dlqOptions.DeadLetterQueueUrl is null)
		{
			return new DeadLetterStatistics { GeneratedAt = DateTimeOffset.UtcNow };
		}

		var attributeRequest = new GetQueueAttributesRequest
		{
			QueueUrl = _dlqOptions.DeadLetterQueueUrl.ToString(),
			AttributeNames =
			[
				"ApproximateNumberOfMessages",
				"ApproximateNumberOfMessagesNotVisible",
			],
		};

		var response = await _sqsClient.GetQueueAttributesAsync(
			attributeRequest, cancellationToken).ConfigureAwait(false);

		var messageCount = 0;
		if (response.Attributes.TryGetValue("ApproximateNumberOfMessages", out var count))
		{
			_ = int.TryParse(count, out messageCount);
		}

		return new DeadLetterStatistics { MessageCount = messageCount, GeneratedAt = DateTimeOffset.UtcNow, };
	}

	/// <inheritdoc />
	async Task<int> IDeadLetterQueueManager.PurgeDeadLetterQueueAsync(
		CancellationToken cancellationToken)
	{
		return await PurgeMessagesAsync(cancellationToken).ConfigureAwait(false);
	}

	#endregion

	#region Private Helpers

	private static string? GetSourceQueueUrl(Message sqsMessage)
	{
		if (sqsMessage.MessageAttributes.TryGetValue(DlqOriginalQueueUrlAttribute, out var attr))
		{
			return attr.StringValue;
		}

		return null;
	}

	private static DlqMessage ConvertToDlqMessage(Message sqsMessage)
	{
		var dlqMessage = new DlqMessage
		{
			MessageId = sqsMessage.MessageId,
			Body = sqsMessage.Body,
			ReceiptHandle = sqsMessage.ReceiptHandle,
		};

		if (sqsMessage.MessageAttributes.TryGetValue(DlqOriginalQueueUrlAttribute, out var queueUrl))
		{
			dlqMessage.SourceQueueUrl = new Uri(queueUrl.StringValue);
		}

		if (sqsMessage.MessageAttributes.TryGetValue(DlqAttemptCountAttribute, out var attemptCount) &&
			int.TryParse(attemptCount.StringValue, out var count))
		{
			dlqMessage.AttemptCount = count;
		}

		if (sqsMessage.MessageAttributes.TryGetValue(DlqReasonAttribute, out var reason))
		{
			dlqMessage.DlqReason = reason.StringValue;
		}

		return dlqMessage;
	}

	private static DeadLetterMessage ConvertToDeadLetterMessage(Message sqsMessage)
	{
		var body = Encoding.UTF8.GetBytes(sqsMessage.Body);
		var cloudMessage = new TransportMessage
		{
			Id = sqsMessage.MessageId,
			Body = body,
			Properties = { ["dispatch.lock.token"] = sqsMessage.ReceiptHandle },
		};

		var reason = "Unknown";
		if (sqsMessage.MessageAttributes.TryGetValue(DlqReasonAttribute, out var dlqReason))
		{
			reason = dlqReason.StringValue;
		}

		var deadLetteredAt = DateTimeOffset.UtcNow;
		if (sqsMessage.MessageAttributes.TryGetValue(DlqMovedAtAttribute, out var movedAt) &&
			DateTimeOffset.TryParse(movedAt.StringValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
		{
			deadLetteredAt = parsed;
		}

		var deliveryAttempts = 0;
		if (sqsMessage.MessageAttributes.TryGetValue(DlqAttemptCountAttribute, out var attempts) &&
			int.TryParse(attempts.StringValue, out var attemptCount))
		{
			deliveryAttempts = attemptCount;
		}

		string? originalSource = null;
		if (sqsMessage.MessageAttributes.TryGetValue(DlqOriginalQueueUrlAttribute, out var sourceUrl))
		{
			originalSource = sourceUrl.StringValue;
		}

		return new DeadLetterMessage
		{
			OriginalMessage = cloudMessage,
			Reason = reason,
			DeliveryAttempts = deliveryAttempts,
			DeadLetteredAt = deadLetteredAt,
			OriginalSource = originalSource,
		};
	}

	#endregion

	#region LoggerMessage Source Generation

	[LoggerMessage(EventId = 1300, Level = LogLevel.Information,
		Message = "Processing DLQ message '{MessageId}'")]
	private static partial void LogProcessingMessage(ILogger logger, string messageId);

	[LoggerMessage(EventId = 1301, Level = LogLevel.Error,
		Message = "Failed to process DLQ message '{MessageId}'")]
	private static partial void LogProcessingFailed(ILogger logger, string messageId, Exception ex);

	[LoggerMessage(EventId = 1302, Level = LogLevel.Information,
		Message = "DLQ message '{MessageId}' processed in {ElapsedMs}ms")]
	private static partial void LogMessageProcessed(ILogger logger, string messageId, long elapsedMs);

	[LoggerMessage(EventId = 1303, Level = LogLevel.Warning,
		Message = "DLQ message '{MessageId}' exhausted retries after {AttemptCount} attempts")]
	private static partial void LogMessageExhaustedRetries(ILogger logger, string messageId, int attemptCount);

	[LoggerMessage(EventId = 1304, Level = LogLevel.Warning,
		Message = "No DLQ URL configured — operation skipped")]
	private static partial void LogNoDlqConfigured(ILogger logger);

	[LoggerMessage(EventId = 1305, Level = LogLevel.Warning,
		Message = "Moving message '{MessageId}' to DLQ: {Reason}")]
	private static partial void LogMovingToDlq(ILogger logger, string messageId, string reason);

	[LoggerMessage(EventId = 1306, Level = LogLevel.Information,
		Message = "Message '{MessageId}' moved to DLQ")]
	private static partial void LogMovedToDlq(ILogger logger, string messageId);

	[LoggerMessage(EventId = 1307, Level = LogLevel.Information,
		Message = "Redriving up to {MaxMessages} messages from DLQ")]
	private static partial void LogRedrivingMessages(ILogger logger, int maxMessages);

	[LoggerMessage(EventId = 1308, Level = LogLevel.Information,
		Message = "Redrove {Count} messages from DLQ")]
	private static partial void LogRedrivenMessages(ILogger logger, int count);

	[LoggerMessage(EventId = 1309, Level = LogLevel.Warning,
		Message = "No source queue URL for message '{MessageId}' — cannot redrive")]
	private static partial void LogNoSourceQueueUrl(ILogger logger, string messageId);

	[LoggerMessage(EventId = 1310, Level = LogLevel.Information,
		Message = "Processing batch of {BatchSize} messages from DLQ")]
	private static partial void LogProcessingBatch(ILogger logger, int batchSize);

	[LoggerMessage(EventId = 1311, Level = LogLevel.Information,
		Message = "Batch processed: {ProcessedCount}/{TotalCount} messages")]
	private static partial void LogBatchProcessed(ILogger logger, int processedCount, int totalCount);

	[LoggerMessage(EventId = 1312, Level = LogLevel.Warning,
		Message = "Purging all messages from DLQ")]
	private static partial void LogPurgingDlq(ILogger logger);

	[LoggerMessage(EventId = 1313, Level = LogLevel.Warning,
		Message = "Purged approximately {Count} messages from DLQ")]
	private static partial void LogPurgedDlq(ILogger logger, int count);

	#endregion

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}
}
