// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.DynamoDb;

public sealed partial class DynamoDbOutboxStore
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.DynamoDbOutboxStoreInitializing, LogLevel.Information,
		"Initializing DynamoDB outbox store with table '{TableName}'")]
	private partial void LogInitializing(string tableName);

	[LoggerMessage(OutboxEventId.DynamoDbOutboxOperationCompleted, LogLevel.Debug,
		"DynamoDB outbox operation '{Operation}' completed. Consumed capacity: {ConsumedCapacity} WCU")]
	private partial void LogOperationCompleted(string operation, double consumedCapacity);

	[LoggerMessage(OutboxEventId.DynamoDbOutboxOperationFailed, LogLevel.Error,
		"DynamoDB outbox operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception? exception);
}
