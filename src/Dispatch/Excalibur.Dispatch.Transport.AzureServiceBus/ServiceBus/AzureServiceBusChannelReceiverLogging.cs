// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// High-performance logging for Azure Service Bus channel receiver hot paths.
/// </summary>
internal static partial class AzureServiceBusChannelReceiverLogging
{
	// Source-generated logging methods (Sprint 362 - EventId Migration)

	/// <summary>
	/// Logs a batch of messages produced from Azure Service Bus.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.BatchProduced, LogLevel.Debug,
		"Produced batch of {Count} messages from Azure Service Bus")]
	public static partial void LogBatchProduced(this ILogger logger, int count);

	/// <summary>
	/// Logs a message completed in Service Bus.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageCompleted, LogLevel.Debug,
		"Message {MessageId} completed in Service Bus")]
	public static partial void LogMessageCompleted(this ILogger logger, string messageId);

	/// <summary>
	/// Logs a message abandoned, will be redelivered.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageAbandoned, LogLevel.Warning,
		"Message {MessageId} abandoned, will be redelivered")]
	public static partial void LogMessageAbandoned(this ILogger logger, string messageId);

	/// <summary>
	/// Logs a message sent to dead letter queue after delivery attempts.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageDeadLettered, LogLevel.Warning,
		"Message {MessageId} sent to dead letter queue after {DeliveryCount} attempts")]
	public static partial void LogMessageDeadLettered(this ILogger logger, string messageId, int deliveryCount);

	/// <summary>
	/// Logs batch processing completed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.BatchProcessingCompleted, LogLevel.Debug,
		"Batch processing completed: {SuccessCount} succeeded, {FailureCount} failed")]
	public static partial void LogBatchCompleted(this ILogger logger, int successCount, int failureCount);

	/// <summary>
	/// Logs Service Bus processor started.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.ProcessorStarted, LogLevel.Information,
		"Service Bus processor started for entity {EntityPath} with ID {ProcessorId}")]
	public static partial void LogProcessorStarted(this ILogger logger, string entityPath, string processorId);

	/// <summary>
	/// Logs Service Bus processor stopped.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.ProcessorStopped, LogLevel.Information,
		"Service Bus processor stopped for entity {EntityPath}")]
	public static partial void LogProcessorStopped(this ILogger logger, string entityPath);

	/// <summary>
	/// Logs Service Bus processor error.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.ProcessorError, LogLevel.Error,
		"Service Bus processor error for entity {EntityPath}")]
	public static partial void LogProcessorError(this ILogger logger, string entityPath, Exception ex);

	/// <summary>
	/// Logs failed message processing.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageProcessingFailed, LogLevel.Error,
		"Failed to process message {MessageId}")]
	public static partial void LogMessageProcessingFailed(this ILogger logger, string messageId, Exception ex);

	/// <summary>
	/// Logs session accepted for processing.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.SessionAccepted, LogLevel.Debug,
		"Session {SessionId} accepted for processing")]
	public static partial void LogSessionAccepted(this ILogger logger, string sessionId);

	/// <summary>
	/// Logs session released.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.SessionReleased, LogLevel.Debug,
		"Session {SessionId} released")]
	public static partial void LogSessionReleased(this ILogger logger, string sessionId);

	/// <summary>
	/// Logs session state updated.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.SessionStateUpdated, LogLevel.Debug,
		"Session {SessionId} state updated")]
	public static partial void LogSessionStateUpdated(this ILogger logger, string sessionId);

	/// <summary>
	/// Logs prefetch count adjusted.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.PrefetchCountAdjusted, LogLevel.Debug,
		"Prefetch count adjusted to {PrefetchCount}")]
	public static partial void LogPrefetchCountAdjusted(this ILogger logger, int prefetchCount);

	/// <summary>
	/// Logs max concurrent calls adjusted.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MaxConcurrentCallsAdjusted, LogLevel.Debug,
		"Max concurrent calls adjusted to {MaxConcurrentCalls}")]
	public static partial void LogMaxConcurrentCallsAdjusted(this ILogger logger, int maxConcurrentCalls);

	/// <summary>
	/// Logs message lock renewed.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageLockRenewed, LogLevel.Debug,
		"Message {MessageId} lock renewed")]
	public static partial void LogMessageLockRenewed(this ILogger logger, string messageId);

	/// <summary>
	/// Logs failed message lock renewal.
	/// </summary>
	[LoggerMessage(AzureServiceBusEventId.MessageLockRenewalFailed, LogLevel.Warning,
		"Failed to renew lock for message {MessageId}")]
	public static partial void LogMessageLockRenewalFailed(this ILogger logger, string messageId, Exception ex);
}
