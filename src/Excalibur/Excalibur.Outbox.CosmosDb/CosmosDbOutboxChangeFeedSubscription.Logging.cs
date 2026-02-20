// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.CosmosDb;

public sealed partial class CosmosDbOutboxChangeFeedSubscription
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.CosmosDbChangeFeedStarting, LogLevel.Information,
		"Starting Cosmos DB outbox change feed subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(OutboxEventId.CosmosDbChangeFeedStopping, LogLevel.Information,
		"Stopping Cosmos DB outbox change feed subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(OutboxEventId.CosmosDbChangeFeedBatchReceived, LogLevel.Debug,
		"Outbox change feed '{SubscriptionId}' received batch of {Count} messages")]
	private partial void LogReceivedBatch(string subscriptionId, int count);
}
