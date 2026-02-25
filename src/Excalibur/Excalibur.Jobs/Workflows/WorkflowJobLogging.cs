// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Source-generated logging methods for <see cref="WorkflowJob{TWorkflow, TInput, TOutput}" />.
/// </summary>
internal static partial class WorkflowJobLog
{
	[LoggerMessage(JobsEventId.WorkflowJobStarting, LogLevel.Information,
		"Starting workflow execution for {WorkflowType} with instance ID {InstanceId}")]
	public static partial void JobStarting(ILogger logger, string workflowType, string instanceId);

	[LoggerMessage(JobsEventId.WorkflowJobCompleted, LogLevel.Information,
		"Workflow {WorkflowType} completed successfully for instance {InstanceId}")]
	public static partial void JobCompleted(ILogger logger, string workflowType, string instanceId);

	[LoggerMessage(JobsEventId.WorkflowJobFailed, LogLevel.Error,
		"Workflow {WorkflowType} failed for instance {InstanceId}: {Status}")]
	public static partial void JobFailed(ILogger logger, Exception? exception, string workflowType, string instanceId, string status);

	[LoggerMessage(JobsEventId.WorkflowJobUnhandledException, LogLevel.Error,
		"Unhandled exception in workflow {WorkflowType} for instance {InstanceId}")]
	public static partial void UnhandledException(ILogger logger, Exception exception, string workflowType, string instanceId);
}
