// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.CosmosDb;

public sealed partial class CosmosDbChangeFeedSubscription<TDocument>
{
	// Source-generated logging methods

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedSubscriptionStarting, LogLevel.Information,
		"Starting Cosmos DB change feed subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedSubscriptionStopping, LogLevel.Information,
		"Stopping Cosmos DB change feed subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(DataCosmosDbEventId.ChangeFeedReceivedBatch, LogLevel.Debug,
		"Change feed '{SubscriptionId}' received batch of {Count} documents")]
	private partial void LogReceivedBatch(string subscriptionId, int count);
}
