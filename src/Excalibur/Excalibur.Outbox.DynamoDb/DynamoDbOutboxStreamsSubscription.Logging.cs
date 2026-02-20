// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.DynamoDb;

public sealed partial class DynamoDbOutboxStreamsSubscription
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.DynamoDbStreamsStarting, LogLevel.Information,
		"Starting DynamoDB outbox streams subscription '{SubscriptionId}'")]
	private partial void LogStarting(string subscriptionId);

	[LoggerMessage(OutboxEventId.DynamoDbStreamsStopping, LogLevel.Information,
		"Stopping DynamoDB outbox streams subscription '{SubscriptionId}'")]
	private partial void LogStopping(string subscriptionId);

	[LoggerMessage(OutboxEventId.DynamoDbStreamsBatchReceived, LogLevel.Debug,
		"DynamoDB outbox streams '{SubscriptionId}' received {TotalCount} records, {UnpublishedCount} unpublished")]
	private partial void LogReceivedBatch(string subscriptionId, int totalCount, int unpublishedCount);
}
