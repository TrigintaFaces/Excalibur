// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

internal static partial class ProjectionRebuildJobLog
{
	[LoggerMessage(JobsEventId.ProjectionRebuildJobStarting, LogLevel.Information, "Starting projection rebuild job.")]
	public static partial void JobStarting(ILogger logger);

	[LoggerMessage(JobsEventId.ProjectionRebuildProcessorMissing, LogLevel.Warning, "No IMaterializedViewProcessor found, skipping projection rebuild.")]
	public static partial void ProcessorMissing(ILogger logger);

	[LoggerMessage(JobsEventId.ProjectionRebuildJobCompleted, LogLevel.Information, "Projection rebuild job completed successfully.")]
	public static partial void JobCompleted(ILogger logger);

	[LoggerMessage(JobsEventId.ProjectionRebuildJobFailed, LogLevel.Error, "Unexpected error in projection rebuild job.")]
	public static partial void JobFailed(ILogger logger, Exception exception);
}
