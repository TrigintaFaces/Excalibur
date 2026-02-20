// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Outbox;

internal static partial class OutboxJobLog
{
	[LoggerMessage(JobsEventId.OutboxJobExecutionStarting, LogLevel.Information, "Starting execution of {JobGroup}:{JobName}.")]
	public static partial void ExecutionStarting(ILogger logger, string jobGroup, string jobName);

	[LoggerMessage(JobsEventId.OutboxJobExecutionCompleted, LogLevel.Information, "Completed execution of {JobGroup}:{JobName}.")]
	public static partial void ExecutionCompleted(ILogger logger, string jobGroup, string jobName);

	[LoggerMessage(JobsEventId.OutboxJobExecutionFailed, LogLevel.Error, "{Error} executing {JobGroup}:{JobName}: {Message}")]
	public static partial void ExecutionFailed(
		ILogger logger,
		Exception exception,
		string error,
		string jobGroup,
		string jobName,
		string message);
}
