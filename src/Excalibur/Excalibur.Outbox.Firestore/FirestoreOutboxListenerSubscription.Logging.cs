// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Firestore;

public sealed partial class FirestoreOutboxListenerSubscription
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.FirestoreListenerStarting, LogLevel.Information,
		"Starting Firestore outbox listener subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(OutboxEventId.FirestoreListenerStopping, LogLevel.Information,
		"Stopping Firestore outbox listener subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(OutboxEventId.FirestoreListenerBatchReceived, LogLevel.Debug,
		"Firestore outbox listener '{SubscriptionId}' received {TotalCount} changes, {UnpublishedCount} unpublished inserts")]
	private partial void LogReceivedBatch(string subscriptionId, int totalCount, int unpublishedCount);
}
