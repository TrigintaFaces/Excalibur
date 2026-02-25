// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

internal static partial class HealthCheckJobLog
{
	[LoggerMessage(JobsEventId.HealthCheckJobStarting, LogLevel.Debug, "Starting health check job")]
	public static partial void JobStarting(ILogger logger);

	[LoggerMessage(JobsEventId.HealthCheckJobCompleted, LogLevel.Information, "Health check completed with status {Status}, duration {DurationMs}ms")]
	public static partial void JobCompleted(ILogger logger, HealthStatus status, double durationMs);

	[LoggerMessage(JobsEventId.HealthCheckWarning, LogLevel.Warning, "Health check {Name} is {Status}: {Description}")]
	public static partial void HealthCheckWarning(ILogger logger, string name, HealthStatus status, string description);

	[LoggerMessage(JobsEventId.HealthCheckError, LogLevel.Error, "Health check {Name} threw exception")]
	public static partial void HealthCheckError(ILogger logger, Exception exception, string name);

	[LoggerMessage(JobsEventId.HealthCheckData, LogLevel.Debug, "Health check {Name} data: {Key}={Value}")]
	public static partial void HealthCheckData(ILogger logger, string name, string key, object? value);

	[LoggerMessage(JobsEventId.HealthCheckJobFailed, LogLevel.Error, "Unexpected error in health check job")]
	public static partial void JobFailed(ILogger logger, Exception exception);
}
