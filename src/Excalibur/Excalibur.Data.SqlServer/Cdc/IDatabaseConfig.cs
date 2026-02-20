// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Represents the configuration settings for a database used in Change Data Capture (CDC) processing.
/// </summary>
public interface IDatabaseConfig
{
	/// <summary>
	/// Gets the name of the database being processed.
	/// </summary>
	/// <value>
	/// The name of the database being processed.
	/// </value>
	string DatabaseName { get; }

	/// <summary>
	/// Gets the unique identifier for the database connection.
	/// </summary>
	/// <value>
	/// The unique identifier for the database connection.
	/// </value>
	/// <remarks> This identifier is used to differentiate between multiple database connections. </remarks>
	string DatabaseConnectionIdentifier { get; }

	/// <summary>
	/// Gets the unique identifier for the connection to the state store database.
	/// </summary>
	/// <value>
	/// The unique identifier for the connection to the state store database.
	/// </value>
	/// <remarks> The state store database is used to persist CDC processing state. </remarks>
	string StateConnectionIdentifier { get; }

	/// <summary>
	/// Gets a value indicating whether processing should stop when a table handler is missing.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if processing should stop when a table handler is missing; otherwise, <c>false</c>.
	/// </value>
	/// <remarks>
	/// If <c> true </c>, the processing will stop and throw an exception when a table does not have a registered handler. If <c> false
	/// </c>, processing will continue despite missing table handlers.
	/// </remarks>
	bool StopOnMissingTableHandler { get; }

	/// <summary>
	/// Gets the list of CDC capture instances to process.
	/// </summary>
	/// <value>
	/// The list of CDC capture instances to process.
	/// </value>
	/// <remarks> Each capture instance corresponds to a table or set of tables tracked by CDC in the database. </remarks>
	string[] CaptureInstances { get; }

	/// <summary>
	/// Gets the batch time interval (in milliseconds) for processing changes.
	/// </summary>
	/// <value>
	/// The batch time interval (in milliseconds) for processing changes.
	/// </value>
	int BatchTimeInterval { get; }

	/// <summary>
	/// Gets the size of the in-memory data queue.
	/// </summary>
	/// <value>
	/// The size of the in-memory data queue.
	/// </value>
	int QueueSize { get; }

	/// <summary>
	/// Gets the batch size used in the producer loop.
	/// </summary>
	/// <value>
	/// The batch size used in the producer loop.
	/// </value>
	int ProducerBatchSize { get; }

	/// <summary>
	/// Gets the batch size used in the consumer loop.
	/// </summary>
	/// <value>
	/// The batch size used in the consumer loop.
	/// </value>
	int ConsumerBatchSize { get; }

	/// <summary>
	/// Gets the recovery options for handling stale CDC position scenarios.
	/// </summary>
	/// <value>
	/// The recovery options, or <see langword="null"/> to use default behavior.
	/// </value>
	/// <remarks>
	/// When <see langword="null"/>, the processor uses <see cref="StalePositionRecoveryStrategy.FallbackToEarliest"/>
	/// as the default strategy.
	/// </remarks>
	CdcRecoveryOptions? RecoveryOptions { get; }
}
