// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

public sealed partial class DynamoDbHealthCheck
{
	// Source-generated logging methods

	[LoggerMessage(DataDynamoDbEventId.HealthCheckStarted, LogLevel.Debug,
		"Starting DynamoDB health check")]
	private partial void LogHealthCheckStarted();

	[LoggerMessage(DataDynamoDbEventId.HealthCheckCompleted, LogLevel.Debug,
		"DynamoDB health check completed successfully")]
	private partial void LogHealthCheckCompleted();

	[LoggerMessage(DataDynamoDbEventId.HealthCheckFailed, LogLevel.Warning,
		"DynamoDB health check failed: {Message}")]
	private partial void LogHealthCheckFailed(string message, Exception? ex);
}
