// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// High-performance [LoggerMessage] source-generated log methods for partitioned outbox processing.
/// Event IDs 3000-3009.
/// </summary>
internal static partial class Log
{
	[LoggerMessage(EventId = 3000, Level = LogLevel.Information,
		Message = "Partitioned outbox processor starting: {PartitionCount} partitions, {ProcessorsPerPartition} processors each")]
	internal static partial void PartitionedOutboxStarting(
		this ILogger logger, int partitionCount, int processorsPerPartition);

	[LoggerMessage(EventId = 3001, Level = LogLevel.Information,
		Message = "Partitioned outbox processor stopped")]
	internal static partial void PartitionedOutboxStopped(this ILogger logger);

	[LoggerMessage(EventId = 3002, Level = LogLevel.Debug,
		Message = "Partition {PartitionId} processor started")]
	internal static partial void PartitionProcessorStarted(this ILogger logger, int partitionId);

	[LoggerMessage(EventId = 3003, Level = LogLevel.Debug,
		Message = "Partition {PartitionId} dispatched {MessageCount} messages")]
	internal static partial void PartitionDispatched(this ILogger logger, int partitionId, int messageCount);

	[LoggerMessage(EventId = 3004, Level = LogLevel.Error,
		Message = "Error processing partition {PartitionId}")]
	internal static partial void PartitionProcessingError(this ILogger logger, Exception exception, int partitionId);

	[LoggerMessage(EventId = 3005, Level = LogLevel.Debug,
		Message = "Partition {PartitionId} processor stopped")]
	internal static partial void PartitionProcessorStopped(this ILogger logger, int partitionId);
}
