// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Firestore;

public sealed partial class FirestoreOutboxStore
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.FirestoreOutboxStoreInitializing, LogLevel.Information,
		"Initializing Firestore outbox store with collection '{CollectionName}'")]
	private partial void LogInitializing(string collectionName);

	[LoggerMessage(OutboxEventId.FirestoreOutboxOperationCompleted, LogLevel.Debug,
		"Firestore outbox operation '{Operation}' completed")]
	private partial void LogOperationCompleted(string operation);

	[LoggerMessage(OutboxEventId.FirestoreOutboxOperationFailed, LogLevel.Error,
		"Firestore outbox operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception? exception);
}
