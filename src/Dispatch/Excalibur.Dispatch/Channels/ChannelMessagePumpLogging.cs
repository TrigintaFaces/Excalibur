// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// High-performance logging extensions for channel message pumps.
/// </summary>
internal static partial class ChannelMessagePumpLogging
{
	[LoggerMessage(CoreEventId.MessagePumpStarting, LogLevel.Information,
		"Starting channel message pump '{Name}'")]
	public static partial void LogPumpStarting(this ILogger logger, string name);

	[LoggerMessage(CoreEventId.MessagePumpStarted, LogLevel.Information,
		"Channel message pump '{Name}' started successfully")]
	public static partial void LogPumpStarted(this ILogger logger, string name);

	[LoggerMessage(CoreEventId.MessagePumpStopping, LogLevel.Information,
		"Stopping channel message pump '{Name}'")]
	public static partial void LogPumpStopping(this ILogger logger, string name);

	[LoggerMessage(CoreEventId.MessagePumpStopped, LogLevel.Information,
		"Channel message pump '{Name}' stopped. Produced: {ProducedCount}, Consumed: {ConsumedCount}, Failed: {FailedCount}")]
	public static partial void LogPumpStopped(this ILogger logger, string name, long producedCount, long consumedCount, long failedCount);

	[LoggerMessage(CoreEventId.ProducerFailed, LogLevel.Error,
		"Producer task failed for message pump '{Name}'")]
	public static partial void LogProducerFailed(this ILogger logger, Exception exception, string name);

	[LoggerMessage(CoreEventId.ProducerTimeout, LogLevel.Warning,
		"Producer task did not complete within timeout for message pump '{Name}'")]
	public static partial void LogProducerTimeout(this ILogger logger, string name);

	[LoggerMessage(CoreEventId.ChannelCreated, LogLevel.Debug,
		"Created bounded channel for message pump '{Name}' with capacity {Capacity}, full mode {FullMode}")]
	public static partial void LogChannelCreated(this ILogger logger, string name, int capacity, BoundedChannelFullMode fullMode);

	[LoggerMessage(CoreEventId.MessageAcknowledged, LogLevel.Trace,
		"Message {MessageId} acknowledged")]
	public static partial void LogMessageAcknowledged(this ILogger logger, string messageId);

	[LoggerMessage(CoreEventId.MessageRejected, LogLevel.Trace,
		"Message {MessageId} rejected: {Reason}")]
	public static partial void LogMessageRejected(this ILogger logger, string messageId, string? reason);

	[LoggerMessage(CoreEventId.ChannelClosed, LogLevel.Debug,
		"Channel closed during message production")]
	public static partial void LogChannelClosed(this ILogger logger);

	[LoggerMessage(CoreEventId.BatchProduced, LogLevel.Debug,
		"Batch of {Count} messages produced to channel")]
	public static partial void LogBatchProduced(this ILogger logger, int count);

	[LoggerMessage(CoreEventId.ChannelFull, LogLevel.Warning,
		"Channel is full, waiting for capacity. Current depth: {CurrentDepth}")]
	public static partial void LogChannelFull(this ILogger logger, int currentDepth);
}
