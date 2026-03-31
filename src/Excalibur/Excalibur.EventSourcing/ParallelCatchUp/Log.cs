// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// High-performance [LoggerMessage] source-generated log methods for parallel catch-up processing.
/// Event IDs 3010-3019.
/// </summary>
internal static partial class Log
{
	[LoggerMessage(EventId = 3010, Level = LogLevel.Debug,
		Message = "Parallel catch-up worker {WorkerId} processing range [{StartPosition}..{EndPosition}]")]
	internal static partial void WorkerProcessingRange(
		this ILogger logger, int workerId, long startPosition, long endPosition);

	[LoggerMessage(EventId = 3011, Level = LogLevel.Debug,
		Message = "Parallel catch-up worker {WorkerId} completed range [{StartPosition}..{EndPosition}], processed {EventCount} events")]
	internal static partial void WorkerCompletedRange(
		this ILogger logger, int workerId, long startPosition, long endPosition, int eventCount);
}
