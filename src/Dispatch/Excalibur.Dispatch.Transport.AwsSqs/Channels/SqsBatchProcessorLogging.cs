// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-performance logging for SQS batch processor hot paths.
/// </summary>
internal static partial class SqsBatchProcessorLogging
{
	// Source-generated logging methods

	/// <summary>
	/// Message processing hot path logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchProcessorMessageError, LogLevel.Error,
		"Error processing message {MessageId}")]
	public static partial void LogMessageProcessingError(this ILogger logger, string messageId, Exception ex);

	/// <summary>
	/// Batch send operation logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchProcessorSendFailure, LogLevel.Warning,
		"Failed to send message in batch: {Code} - {ErrorMessage}")]
	public static partial void LogBatchSendFailure(this ILogger logger, string code, string errorMessage);

	[LoggerMessage(AwsSqsEventId.BatchProcessorSendFlushError, LogLevel.Error,
		"Error flushing send batch")]
	public static partial void LogBatchSendFlushError(this ILogger logger, Exception ex);

	/// <summary>
	/// Batch delete operation logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchProcessorDeleteFailure, LogLevel.Warning,
		"Failed to delete message in batch: {Code} - {ErrorMessage}")]
	public static partial void LogBatchDeleteFailure(this ILogger logger, string code, string errorMessage);

	/// <summary>
	/// Batch processing metrics logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchProcessorProcessed, LogLevel.Debug,
		"Batch processed: {SuccessCount} succeeded, {FailureCount} failed")]
	public static partial void LogBatchProcessed(this ILogger logger, int successCount, int failureCount);

	[LoggerMessage(AwsSqsEventId.BatchProcessorSent, LogLevel.Debug,
		"Batch of {MessageCount} messages sent to SQS")]
	public static partial void LogBatchSent(this ILogger logger, int messageCount);

	[LoggerMessage(AwsSqsEventId.BatchProcessorDeleted, LogLevel.Debug,
		"Batch of {MessageCount} messages deleted from SQS")]
	public static partial void LogBatchDeleted(this ILogger logger, int messageCount);

	/// <summary>
	/// Batch accumulation logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchProcessorAccumulating, LogLevel.Trace,
		"Accumulating batch: {CurrentSize}/{MaxSize}")]
	public static partial void LogBatchAccumulating(this ILogger logger, int currentSize, int maxSize);

	[LoggerMessage(AwsSqsEventId.BatchProcessorFlushTriggered, LogLevel.Debug,
		"Batch flush triggered with {MessageCount} messages")]
	public static partial void LogBatchFlushTriggered(this ILogger logger, int messageCount);
}
