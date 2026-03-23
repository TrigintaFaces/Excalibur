// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.GoogleCloud.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.GoogleCloud;

public sealed partial class GoogleCloudSchedulerJobProvider
{
	// Source-generated logging methods

	[LoggerMessage(GoogleCloudJobsEventId.GoogleCloudSchedulerJobCreated, LogLevel.Information,
		"Successfully created Google Cloud Scheduler job {JobName} for job type {JobType}")]
	private partial void LogCreatedJobSuccess(string jobName, string jobType);

	[LoggerMessage(GoogleCloudJobsEventId.GoogleCloudSchedulerJobCreationFailed, LogLevel.Error,
		"Failed to create Google Cloud Scheduler job {JobName} for job type {JobType}")]
	private partial void LogFailedToCreateJob(string jobName, string jobType, Exception? exception);

	[LoggerMessage(GoogleCloudJobsEventId.GoogleCloudSchedulerJobDeleted, LogLevel.Information,
		"Successfully deleted Google Cloud Scheduler job {JobName}")]
	private partial void LogDeletedJobSuccess(string jobName);

	[LoggerMessage(GoogleCloudJobsEventId.GoogleCloudSchedulerJobNotFound, LogLevel.Warning,
		"Google Cloud Scheduler job {JobName} not found for deletion")]
	private partial void LogJobNotFoundForDeletion(string jobName);

	[LoggerMessage(GoogleCloudJobsEventId.GoogleCloudSchedulerJobDeletionFailed, LogLevel.Error,
		"Failed to delete Google Cloud Scheduler job {JobName}")]
	private partial void LogFailedToDeleteJob(string jobName, Exception? exception);
}
