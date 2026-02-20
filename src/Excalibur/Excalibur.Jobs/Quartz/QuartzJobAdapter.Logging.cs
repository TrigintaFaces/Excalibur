// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.Quartz;

internal static partial class QuartzJobAdapterLog
{
	[LoggerMessage(JobsEventId.JobTypeNotFoundOrInvalid, LogLevel.Error, "JobType not found or invalid in JobDataMap for job {JobKey}. Value: {JobTypeData}")]
	public static partial void JobTypeNotFoundOrInvalid(ILogger logger, JobKey jobKey, object? jobTypeData);

	[LoggerMessage(JobsEventId.CouldNotResolveJobType, LogLevel.Error, "Could not resolve job of type {JobType} from DI container")]
	public static partial void CouldNotResolveJobType(ILogger logger, Type jobType);

	[LoggerMessage(JobsEventId.ExecutingJob, LogLevel.Information, "Executing job {JobType} with key {JobKey}")]
	public static partial void ExecutingJob(ILogger logger, string jobType, JobKey jobKey);

	[LoggerMessage(JobsEventId.JobDoesNotImplementInterface, LogLevel.Error, "Job {JobType} does not implement IBackgroundJob")]
	public static partial void JobDoesNotImplementInterface(ILogger logger, Type jobType);

	[LoggerMessage(JobsEventId.JobCompletedSuccessfully, LogLevel.Information, "Job {JobType} with key {JobKey} completed successfully")]
	public static partial void JobCompletedSuccessfully(ILogger logger, string jobType, JobKey jobKey);

	[LoggerMessage(JobsEventId.ErrorExecutingJob, LogLevel.Error, "Error executing job {JobType} with key {JobKey}")]
	public static partial void JobExecutionFailed(ILogger logger, Exception exception, string jobType, JobKey jobKey);
}
