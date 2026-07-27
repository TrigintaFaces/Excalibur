// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Defines the contract for managing the state of Change Data Capture (CDC) processing.
/// </summary>
/// <remarks>
/// Extends the provider-neutral <see cref="ICdcStateStore" /> so SQL Server CDC state stores are
/// conformable to the generic consumer-checkpoint contract (crash recovery, consumer tracking,
/// progress monitoring) in addition to the SQL Server-specific LSN/sequence-value surface. The
/// generic checkpoint rows share the same table, distinguished by empty database/table
/// discriminators — mirroring the adapter pattern used by the Postgres, MongoDB, CosmosDB,
/// DynamoDB, and Firestore state stores.
/// </remarks>
public interface ISqlServerCdcStateStore : ICdcStateStore, IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Retrieves the last processed position for a specified database connection and name.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The unique identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database to retrieve the processing state for. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	/// A task that represents the asynchronous operation, containing a collection of the last processed
	/// <see cref="CdcProcessingState" /> for each capture instance.
	/// </returns>
	Task<IEnumerable<CdcProcessingState>> GetLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the last processed position for a specified database connection and name.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The unique identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database to update the processing state for. </param>
	/// <param name="tableName"> The name of the table to update the processing state for. </param>
	/// <param name="position"> The LSN position to set as the last processed position. </param>
	/// <param name="sequenceValue"> The sequence value associated with the last processed position, if any. </param>
	/// <param name="commitTime"> The commit time of the last processed LSN. </param>
	/// <param name="fencingToken">
	/// The current leadership tenure's fencing token, or <see langword="null"/> when leader-election fencing
	/// is not configured. When non-null, the checkpoint write is a non-decreasing compare-and-swap: it is
	/// applied only if the stored token is less than or equal to this token (a demoted leader presenting an
	/// older token is rejected, leaving the LSN checkpoint unchanged — no regression or skip). The return
	/// value is the number of rows written, so <c>0</c> with a non-null token signals a superseded leader.
	/// </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The number of rows written (<c>0</c> when a non-null fencing token was superseded). </returns>
	Task<int> UpdateLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		string tableName,
		byte[] position,
		byte[]? sequenceValue,
		DateTime? commitTime,
		long? fencingToken,
		CancellationToken cancellationToken);
}
