// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.CosmosDb;

public sealed partial class CosmosDbOutboxStore
{
	// Source-generated logging methods

	[LoggerMessage(OutboxEventId.CosmosDbOutboxStoreInitializing, LogLevel.Information,
		"Initializing Cosmos DB outbox store with container '{ContainerName}'")]
	private partial void LogInitializing(string containerName);

	[LoggerMessage(OutboxEventId.CosmosDbOutboxOperationCompleted, LogLevel.Debug,
		"Cosmos DB outbox operation '{Operation}' completed. Request charge: {RequestCharge} RU")]
	private partial void LogOperationCompleted(string operation, double requestCharge);

	[LoggerMessage(OutboxEventId.CosmosDbOutboxOperationFailed, LogLevel.Error,
		"Cosmos DB outbox operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception? exception);
}
