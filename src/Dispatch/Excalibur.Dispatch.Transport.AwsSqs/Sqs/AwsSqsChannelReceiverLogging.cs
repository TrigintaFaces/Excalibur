// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-performance logging for SQS channel receiver.
/// </summary>
internal static partial class AwsSqsChannelReceiverLogging
{
	// Source-generated logging methods

	[LoggerMessage(AwsSqsEventId.ChannelBatchProduced, LogLevel.Debug,
		"Produced batch of {MessageCount} messages from SQS")]
	public static partial void LogBatchProduced(this ILogger logger, int messageCount);

	[LoggerMessage(AwsSqsEventId.ChannelMessageAcknowledged, LogLevel.Debug,
		"Message {MessageId} acknowledged")]
	public static partial void LogMessageAcknowledged(this ILogger logger, string messageId);

	[LoggerMessage(AwsSqsEventId.ChannelMessageRejected, LogLevel.Warning,
		"Message {MessageId} rejected: {Reason}")]
	public static partial void LogMessageRejected(this ILogger logger, string messageId, string? reason);

	[LoggerMessage(AwsSqsEventId.ChannelMessageEnqueuedForDelete, LogLevel.Trace,
		"Message {MessageId} enqueued for batch delete with request ID {RequestId}")]
	public static partial void LogMessageEnqueuedForBatchDelete(this ILogger logger, string messageId, long requestId);

	[LoggerMessage(AwsSqsEventId.ChannelBatchDeleteCompleted, LogLevel.Debug,
		"Batch delete completed: {SuccessCount} succeeded, {FailureCount} failed")]
	public static partial void LogBatchDeleteCompleted(this ILogger logger, int successCount, int failureCount);

	[LoggerMessage(AwsSqsEventId.ChannelBatchDeleteFailed, LogLevel.Warning,
		"Batch delete failed for request {RequestId}: {ErrorCode} - {ErrorMessage}")]
	public static partial void LogBatchDeleteFailed(this ILogger logger, string requestId, string errorCode, string errorMessage);

	[LoggerMessage(AwsSqsEventId.ChannelMessageConsumed, LogLevel.Trace,
		"Message consumed successfully")]
	public static partial void LogMessageConsumed(this ILogger logger);

	[LoggerMessage(AwsSqsEventId.ChannelMessageFailed, LogLevel.Warning,
		"Message processing failed")]
	public static partial void LogMessageFailed(this ILogger logger);

	[LoggerMessage(AwsSqsEventId.ChannelFailedToDeserializeContext, LogLevel.Warning,
			"Failed to deserialize message context from SQS attributes")]
	public static partial void LogFailedToDeserializeContext(this ILogger logger, Exception ex);

	[LoggerMessage(AwsSqsEventId.SqsMessageDecompressionFailed, LogLevel.Warning,
			"Failed to decompress SQS message body")]
	public static partial void LogMessageDecompressionFailed(this ILogger logger, Exception ex);

	[LoggerMessage(AwsSqsEventId.ChannelFailedToExecuteBatchDelete, LogLevel.Error,
			"Failed to execute batch delete for {EntryCount} entries")]
	public static partial void LogFailedToExecuteBatchDelete(this ILogger logger, int entryCount, Exception ex);
}
