// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore;

public sealed partial class FirestoreHealthCheck
{
	// Source-generated logging methods

	[LoggerMessage(DataFirestoreEventId.HealthCheckStarted, LogLevel.Debug,
		"Starting Firestore health check")]
	private partial void LogHealthCheckStarted();

	[LoggerMessage(DataFirestoreEventId.HealthCheckCompleted, LogLevel.Debug,
		"Firestore health check completed successfully")]
	private partial void LogHealthCheckCompleted();

	[LoggerMessage(DataFirestoreEventId.HealthCheckFailed, LogLevel.Warning,
		"Firestore health check failed: {Message}")]
	private partial void LogHealthCheckFailed(string message, Exception? ex);
}
