// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

public sealed partial class DynamoDbStreamsSubscription<TDocument>
{
	// Source-generated logging methods

	[LoggerMessage(DataDynamoDbEventId.StreamsStarting, LogLevel.Information,
		"Starting DynamoDB Streams subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(DataDynamoDbEventId.StreamsStopping, LogLevel.Information,
		"Stopping DynamoDB Streams subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(DataDynamoDbEventId.StreamsReceivedBatch, LogLevel.Debug,
		"DynamoDB Streams '{SubscriptionId}' received batch of {Count} records")]
	private partial void LogReceivedBatch(string subscriptionId, int count);
}
