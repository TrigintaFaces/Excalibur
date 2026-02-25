// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

public sealed partial class DynamoDbPersistenceProvider
{
	// Source-generated logging methods

	[LoggerMessage(DataDynamoDbEventId.ProviderInitializing, LogLevel.Information,
		"Initializing DynamoDB provider '{Name}'")]
	private partial void LogInitializing(string name);

	[LoggerMessage(DataDynamoDbEventId.ProviderDisposing, LogLevel.Debug,
		"Disposing DynamoDB provider '{Name}'")]
	private partial void LogDisposing(string name);

	[LoggerMessage(DataDynamoDbEventId.OperationCompletedWithCapacity, LogLevel.Debug,
		"DynamoDB operation '{Operation}' completed. Consumed capacity: {ConsumedCapacity} units")]
	private partial void LogOperationCompleted(string operation, double consumedCapacity);

	[LoggerMessage(DataDynamoDbEventId.OperationFailed, LogLevel.Error,
		"DynamoDB operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception ex);
}
