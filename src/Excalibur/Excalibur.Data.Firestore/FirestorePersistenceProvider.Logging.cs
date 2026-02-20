// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore;

public sealed partial class FirestorePersistenceProvider
{
	// Source-generated logging methods

	[LoggerMessage(DataFirestoreEventId.ProviderInitializing, LogLevel.Information,
		"Initializing Firestore provider '{Name}'")]
	private partial void LogInitializing(string name);

	[LoggerMessage(DataFirestoreEventId.ProviderDisposing, LogLevel.Debug,
		"Disposing Firestore provider '{Name}'")]
	private partial void LogDisposing(string name);

	[LoggerMessage(DataFirestoreEventId.OperationCompleted, LogLevel.Debug,
		"Firestore operation '{Operation}' completed")]
	private partial void LogOperationCompleted(string operation);

	[LoggerMessage(DataFirestoreEventId.OperationFailed, LogLevel.Error,
		"Firestore operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception ex);
}
