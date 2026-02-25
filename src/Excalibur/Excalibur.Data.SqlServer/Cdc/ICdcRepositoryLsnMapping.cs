// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides LSN-to-time mapping and advanced LSN navigation for SQL Server CDC.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface separates LSN mapping concerns from the core CDC data retrieval
/// in <see cref="ICdcRepository"/>. Consumers that only need to fetch changes
/// can depend on the smaller <see cref="ICdcRepository"/> interface.
/// </para>
/// </remarks>
public interface ICdcRepositoryLsnMapping
{
	/// <summary>
	/// Retrieves the next Log Sequence Number (LSN) after the specified last processed LSN for a specific capture instance.
	/// </summary>
	/// <param name="captureInstance">The name of the capture instance to query for the next LSN.</param>
	/// <param name="lastProcessedLsn">The LSN from which to find the next LSN.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous operation, containing the next LSN as a nullable byte array, or null if no next LSN is available.</returns>
	Task<byte[]?> GetNextLsnAsync(string captureInstance, byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	/// Maps an LSN to a commit time in the database.
	/// </summary>
	/// <param name="lsn"> The LSN to map to a time. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the mapped commit time as a nullable <see cref="DateTime" />. </returns>
	Task<DateTime?> GetLsnToTimeAsync(byte[] lsn, CancellationToken cancellationToken);

	/// <summary>
	/// Maps a time to an LSN using the specified relational operator.
	/// </summary>
	/// <param name="lsnDate"> The date to map to an LSN. </param>
	/// <param name="relationalOperator">
	/// The relational operator for mapping (e.g., "smallest greater than", "largest less than or equal").
	/// </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the mapped LSN as a nullable byte array. </returns>
	Task<byte[]?> GetTimeToLsnAsync(DateTime lsnDate, string relationalOperator, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the next valid LSN after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next valid LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the next valid LSN as a nullable byte array. </returns>
	Task<byte[]?> GetNextValidLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken);
}
