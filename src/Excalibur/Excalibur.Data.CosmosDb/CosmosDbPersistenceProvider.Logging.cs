// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.CosmosDb;

public sealed partial class CosmosDbPersistenceProvider
{
	// Source-generated logging methods

	[LoggerMessage(DataCosmosDbEventId.ProviderInitializing, LogLevel.Information,
		"Initializing Cosmos DB provider '{Name}'")]
	private partial void LogInitializing(string name);

	[LoggerMessage(DataCosmosDbEventId.ProviderDisposing, LogLevel.Debug,
		"Disposing Cosmos DB provider '{Name}'")]
	private partial void LogDisposing(string name);

	[LoggerMessage(DataCosmosDbEventId.OperationCompletedWithCharge, LogLevel.Debug,
		"Cosmos DB operation '{Operation}' completed. Request charge: {RequestCharge} RU")]
	private partial void LogOperationCompleted(string operation, double requestCharge);

	[LoggerMessage(DataCosmosDbEventId.OperationFailed, LogLevel.Error,
		"Cosmos DB operation '{Operation}' failed: {Message}")]
	private partial void LogOperationFailed(string operation, string message, Exception ex);
}
