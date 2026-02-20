// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaMonitoringService
{
	// Source-generated logging methods

	[LoggerMessage(SagaEventId.RunningCountRetrieved, LogLevel.Debug,
		"Found {Count} running sagas for type {SagaType}")]
	private partial void LogRunningCount(int count, string? sagaType);

	[LoggerMessage(SagaEventId.CompletedCountRetrieved, LogLevel.Debug,
		"Found {Count} completed sagas for type {SagaType}")]
	private partial void LogCompletedCount(int count, string? sagaType);

	[LoggerMessage(SagaEventId.StuckSagasRetrieved, LogLevel.Debug,
		"Found {Count} stuck sagas (threshold: {ThresholdMinutes} minutes)")]
	private partial void LogStuckSagas(int count, double thresholdMinutes);

	[LoggerMessage(SagaEventId.FailedSagasRetrieved, LogLevel.Debug,
		"Found {Count} failed sagas")]
	private partial void LogFailedSagas(int count);

	[LoggerMessage(SagaEventId.AverageCompletionTimeRetrieved, LogLevel.Debug,
		"Average completion time for {SagaType}: {AverageMs} ms")]
	private partial void LogAverageCompletionTime(string sagaType, double? averageMs);
}
