// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
	// Source-generated logging methods

	[LoggerMessage(SagaEventId.TimeoutScheduled, LogLevel.Debug,
		"Scheduled timeout {TimeoutId} for saga {SagaId}")]
	private partial void LogTimeoutScheduled(string timeoutId, string sagaId);

	[LoggerMessage(SagaEventId.TimeoutCancelled, LogLevel.Debug,
		"Cancelled timeout {TimeoutId} for saga {SagaId}")]
	private partial void LogTimeoutCancelled(string timeoutId, string sagaId);

	[LoggerMessage(SagaEventId.AllTimeoutsCancelled, LogLevel.Debug,
		"Cancelled {Count} timeouts for saga {SagaId}")]
	private partial void LogAllTimeoutsCancelled(string sagaId, int count);

	[LoggerMessage(SagaEventId.TimeoutMarkedDelivered, LogLevel.Debug,
		"Marked timeout {TimeoutId} as delivered")]
	private partial void LogTimeoutDelivered(string timeoutId);
}
