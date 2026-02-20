// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// High-performance logging for Azure Storage Queue Consumer using source generators.
/// </summary>
/// <remarks>
/// Migrated to use semantic constants from AzureServiceBusEventId.
/// </remarks>
public static partial class StorageQueueConsumerLogging
{
	/// <summary>
	/// Logs that polling has started for a Storage Queue.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueStartingProcessing, LogLevel.Information,
		"Started polling messages from Storage Queue '{QueueName}'")]
	public static partial void PollingStarted(ILogger logger, string queueName);

	/// <summary>
	/// Logs that polling has stopped for a Storage Queue.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueStoppingProcessing, LogLevel.Information,
		"Stopped polling messages from Storage Queue '{QueueName}'")]
	public static partial void PollingStopped(ILogger logger, string queueName);

	/// <summary>
	/// Logs that a message was received.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageReceived, LogLevel.Debug,
		"Received message '{MessageId}' with pop receipt '{PopReceipt}' and dequeue count {DequeueCount}")]
	public static partial void MessageReceived(ILogger logger, string messageId, string popReceipt, int dequeueCount);

	/// <summary>
	/// Logs that a message was processed successfully.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageProcessed, LogLevel.Debug,
		"Processed message '{MessageId}' in {ElapsedMilliseconds}ms")]
	public static partial void MessageProcessed(ILogger logger, string messageId, long elapsedMilliseconds);

	/// <summary>
	/// Logs that a message processing failed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageProcessingFailed, LogLevel.Error,
		"Failed to process message '{MessageId}' (attempt {DequeueCount})")]
	public static partial void MessageProcessingFailed(ILogger logger, string messageId, int dequeueCount, Exception ex);

	/// <summary>
	/// Logs that a message was deleted successfully.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageDeleted, LogLevel.Debug,
		"Deleted message '{MessageId}' from queue after successful processing")]
	public static partial void MessageDeleted(ILogger logger, string messageId);

	/// <summary>
	/// Logs that a message delete operation failed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageAcknowledged, LogLevel.Warning,
		"Failed to delete message '{MessageId}' from queue")]
	public static partial void MessageDeleteFailed(ILogger logger, string messageId, Exception ex);

	/// <summary>
	/// Logs that a message visibility timeout was updated.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueVisibilityExtended, LogLevel.Debug,
		"Updated visibility timeout for message '{MessageId}' to {TimeoutSeconds} seconds")]
	public static partial void MessageVisibilityUpdated(ILogger logger, string messageId, int timeoutSeconds);

	/// <summary>
	/// Logs that a message visibility timeout update failed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueVisibilityExtensionFailed, LogLevel.Warning,
		"Failed to update visibility timeout for message '{MessageId}'")]
	public static partial void MessageVisibilityUpdateFailed(ILogger logger, string messageId, Exception ex);

	/// <summary>
	/// Logs that a batch of messages was received.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessagesReceived, LogLevel.Debug,
		"Received batch of {Count} messages in {ElapsedMilliseconds}ms")]
	public static partial void BatchReceived(ILogger logger, int count, long elapsedMilliseconds);

	/// <summary>
	/// Logs that a queue was not found.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueInvalidDestination, LogLevel.Error,
		"Storage Queue '{QueueName}' not found")]
	public static partial void QueueNotFound(ILogger logger, string queueName, Exception ex);

	/// <summary>
	/// Logs a connection error while polling.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueReceiveFailed, LogLevel.Warning,
		"Connection error while polling Storage Queue '{QueueName}'")]
	public static partial void ConnectionError(ILogger logger, string queueName, Exception ex);

	/// <summary>
	/// Logs a retry delay before the next polling attempt.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueHealthChecked, LogLevel.Debug,
		"Waiting {DelaySeconds} seconds before retry attempt {Attempt} for queue '{QueueName}'")]
	public static partial void RetryDelay(ILogger logger, string queueName, int delaySeconds, int attempt);

	/// <summary>
	/// Logs that a message was moved to the dead letter queue.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueDeadLettered, LogLevel.Warning,
		"Moving message '{MessageId}' to dead letter queue after exceeding max retries")]
	public static partial void DeadLetterMessage(ILogger logger, string messageId);

	/// <summary>
	/// Logs that a CloudEvent was parsed successfully.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueCloudEventParsed, LogLevel.Debug,
		"Parsed CloudEvent '{EventId}' with type '{EventType}'")]
	public static partial void CloudEventParsed(ILogger logger, string eventId, string eventType);

	/// <summary>
	/// Logs that a CloudEvent parsing failed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueInvalidMessageType, LogLevel.Warning,
		"Failed to parse CloudEvent from message '{MessageId}'")]
	public static partial void CloudEventParsingFailed(ILogger logger, string messageId, Exception ex);

	/// <summary>
	/// Logs a polling cycle completion.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueIterationCompleted, LogLevel.Trace,
		"Completed polling cycle for queue '{QueueName}' in {ElapsedMilliseconds}ms")]
	public static partial void PollingCycle(ILogger logger, string queueName, long elapsedMilliseconds);

	/// <summary>
	/// Logs that a poll returned no messages.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.StorageQueueNoMessages, LogLevel.Trace,
		"No messages available in queue '{QueueName}'")]
	public static partial void EmptyPoll(ILogger logger, string queueName);
}
