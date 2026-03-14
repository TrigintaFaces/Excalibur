// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

internal static partial class SnapshotCreationJobLog
{
	[LoggerMessage(JobsEventId.SnapshotCreationJobStarting, LogLevel.Information, "Starting snapshot creation job for aggregate {AggregateType}.")]
	public static partial void JobStarting(ILogger logger, string aggregateType);

	[LoggerMessage(JobsEventId.SnapshotCreationDependenciesMissing, LogLevel.Warning, "Required dependencies (IEventSourcedRepository or ISnapshotManager) not found for {AggregateType}, skipping snapshot creation.")]
	public static partial void DependenciesMissing(ILogger logger, string aggregateType);

	[LoggerMessage(JobsEventId.SnapshotCreationJobCompleted, LogLevel.Information, "Snapshot creation job completed for {AggregateType}: created {SnapshotCount} snapshots.")]
	public static partial void JobCompleted(ILogger logger, string aggregateType, int snapshotCount);

	[LoggerMessage(JobsEventId.SnapshotCreationJobCancelled, LogLevel.Information, "Snapshot creation job cancelled for {AggregateType}.")]
	public static partial void JobCancelled(ILogger logger, string aggregateType);

	[LoggerMessage(JobsEventId.SnapshotCreationJobFailed, LogLevel.Error, "Unexpected error in snapshot creation job for {AggregateType}.")]
	public static partial void JobFailed(ILogger logger, string aggregateType, Exception exception);
}
