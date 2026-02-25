// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Defines the core contract for retrieving Change Data Capture (CDC) data from SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// This interface contains the 5 essential methods for CDC processing: LSN navigation,
/// position retrieval, change detection, and change fetching. For LSN-to-time mapping
/// and advanced navigation, see <see cref="ICdcRepositoryLsnMapping"/>.
/// </para>
/// </remarks>
public interface ICdcRepository : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Retrieves the next Log Sequence Number (LSN) after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the next LSN as a byte array. </returns>
	Task<byte[]> GetNextLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the minimum LSN for a specific capture instance.
	/// </summary>
	/// <param name="captureInstance"> The capture instance to query. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the minimum LSN as a byte array. </returns>
	Task<byte[]> GetMinPositionAsync(string captureInstance, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the maximum LSN currently available in the database.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the maximum LSN as a byte array. </returns>
	Task<byte[]> GetMaxPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Checks if changes exist between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="fromPosition"> The starting LSN position. </param>
	/// <param name="toPosition"> The ending LSN position. </param>
	/// <param name="captureInstances"> A collection of capture instance names to query for changes. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing a boolean indicating whether changes exist. </returns>
	Task<bool> ChangesExistAsync(
		byte[] fromPosition,
		byte[] toPosition,
		IEnumerable<string> captureInstances,
		CancellationToken cancellationToken);

	/// <summary>
	/// Fetches change rows between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="captureInstance"> A capture instance table name to query for changes. </param>
	/// <param name="batchSize"> The number of records to retrieve in the batch. </param>
	/// <param name="lsn"> The LSN position. </param>
	/// <param name="lastSequenceValue">
	/// The last processed sequence value, if any. This is used for processing changes with finer granularity.
	/// </param>
	/// <param name="lastOperation"> The CDC operation code filter for the types of changes to retrieve. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> An asynchronous stream of <see cref="CdcRow" /> instances representing the captured changes. </returns>
	Task<IEnumerable<CdcRow>> FetchChangesAsync(
		string captureInstance,
		int batchSize,
		byte[] lsn,
		byte[]? lastSequenceValue,
		CdcOperationCodes lastOperation,
		CancellationToken cancellationToken);
}
