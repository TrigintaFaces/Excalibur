// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.CloudProviders.Aws;

public partial class AwsSchedulerJobProvider
{
	// Source-generated logging methods

	[LoggerMessage(JobsEventId.AwsSchedulerScheduleCreated, LogLevel.Information,
		"Successfully created AWS EventBridge schedule {JobName} for job type {JobType}")]
	private partial void LogCreatedScheduleSuccess(string jobName, string jobType);

	[LoggerMessage(JobsEventId.AwsSchedulerScheduleCreationFailed, LogLevel.Error,
		"Failed to create AWS EventBridge schedule {JobName} for job type {JobType}")]
	private partial void LogFailedToCreateSchedule(string jobName, string jobType, Exception? exception);

	[LoggerMessage(JobsEventId.AwsSchedulerScheduleDeleted, LogLevel.Information,
		"Successfully deleted AWS EventBridge schedule {JobName}")]
	private partial void LogDeletedScheduleSuccess(string jobName);

	[LoggerMessage(JobsEventId.AwsSchedulerScheduleNotFound, LogLevel.Warning,
		"AWS EventBridge schedule {JobName} not found for deletion")]
	private partial void LogScheduleNotFoundForDeletion(string jobName);

	[LoggerMessage(JobsEventId.AwsSchedulerScheduleDeletionFailed, LogLevel.Error,
		"Failed to delete AWS EventBridge schedule {JobName}")]
	private partial void LogFailedToDeleteSchedule(string jobName, Exception? exception);
}
