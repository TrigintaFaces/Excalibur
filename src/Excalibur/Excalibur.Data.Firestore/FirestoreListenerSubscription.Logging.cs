// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore;

public sealed partial class FirestoreListenerSubscription<TDocument>
{
	// Source-generated logging methods

	[LoggerMessage(DataFirestoreEventId.ListenerStarting, LogLevel.Information,
		"Starting Firestore listener subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(DataFirestoreEventId.ListenerStopping, LogLevel.Information,
		"Stopping Firestore listener subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(DataFirestoreEventId.ListenerReceivedChanges, LogLevel.Debug,
		"Firestore listener '{SubscriptionId}' received {Count} document changes")]
	private partial void LogReceivedChanges(string subscriptionId, int count);
}
