// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Azure.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Azure;

public sealed partial class AzureLogicAppsJobProvider
{
	// Source-generated logging methods

	[LoggerMessage(AzureJobsEventId.AzureLogicAppsWorkflowCreated, LogLevel.Information,
		"Successfully created Azure Logic App workflow {WorkflowName} for job type {JobType}")]
	private partial void LogCreatedWorkflowSuccess(string workflowName, string jobType);

	[LoggerMessage(AzureJobsEventId.AzureLogicAppsWorkflowCreationFailed, LogLevel.Error,
		"Failed to create Azure Logic App workflow for job {JobName} of type {JobType}")]
	private partial void LogFailedToCreateWorkflow(string jobName, string jobType, Exception? exception);

	[LoggerMessage(AzureJobsEventId.AzureLogicAppsWorkflowDeleted, LogLevel.Information,
		"Successfully deleted Azure Logic App workflow {WorkflowName}")]
	private partial void LogDeletedWorkflowSuccess(string workflowName);

	[LoggerMessage(AzureJobsEventId.AzureLogicAppsWorkflowNotFound, LogLevel.Warning,
		"Azure Logic App workflow {WorkflowName} not found for deletion")]
	private partial void LogWorkflowNotFoundForDeletion(string workflowName);

	[LoggerMessage(AzureJobsEventId.AzureLogicAppsWorkflowDeletionFailed, LogLevel.Error,
		"Failed to delete Azure Logic App workflow for job {JobName}")]
	private partial void LogFailedToDeleteWorkflow(string jobName, Exception? exception);
}
