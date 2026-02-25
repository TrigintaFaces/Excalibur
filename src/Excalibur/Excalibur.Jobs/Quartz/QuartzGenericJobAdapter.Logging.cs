// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.Quartz;

internal static partial class QuartzGenericJobAdapterLog
{
	[LoggerMessage(JobsEventId.GenericContextDeserializationFailed, LogLevel.Error, "Failed to deserialize job context for job {JobKey}")]
	public static partial void ContextDeserializationFailed(ILogger logger, Exception exception, JobKey jobKey);

	[LoggerMessage(JobsEventId.GenericContextNotFoundOrInvalid, LogLevel.Error, "Context not found or invalid in JobDataMap for job {JobKey}")]
	public static partial void ContextNotFoundOrInvalid(ILogger logger, JobKey jobKey);

	[LoggerMessage(JobsEventId.ExecutingGenericJob, LogLevel.Information, "Executing job {JobType} with key {JobKey}")]
	public static partial void ExecutingGenericJob(ILogger logger, string jobType, JobKey jobKey);

	[LoggerMessage(JobsEventId.GenericJobCompletedSuccessfully, LogLevel.Information, "Job {JobType} with key {JobKey} completed successfully")]
	public static partial void GenericJobCompletedSuccessfully(ILogger logger, string jobType, JobKey jobKey);

	[LoggerMessage(JobsEventId.GenericJobExecutionFailed, LogLevel.Error, "Error executing job {JobType} with key {JobKey}")]
	public static partial void GenericJobExecutionFailed(ILogger logger, Exception exception, string jobType, JobKey jobKey);
}
