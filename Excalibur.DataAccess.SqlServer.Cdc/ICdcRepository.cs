// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///   Defines the contract for a repository that interacts with Change Data Capture (CDC) metadata and data.
/// </summary>
public interface ICdcRepository : IAsyncDisposable
{
	/// <summary>
	///   Retrieves the next Log Sequence Number (LSN) after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the next LSN as a byte array. </returns>
	Task<byte[]> GetNextLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken);

	Task<byte[]?> GetNextLsnAsync(string captureInstance, byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	///   Maps an LSN to a commit time in the database.
	/// </summary>
	/// <param name="lsn"> The LSN to map to a time. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///   A task that represents the asynchronous operation, containing the mapped commit time as a nullable <see cref="DateTime" />.
	/// </returns>
	Task<DateTime?> GetLsnToTimeAsync(byte[] lsn, CancellationToken cancellationToken);

	/// <summary>
	///   Maps a time to an LSN using the specified relational operator.
	/// </summary>
	/// <param name="lsnDate"> The date to map to an LSN. </param>
	/// <param name="relationalOperator">
	///   The relational operator for mapping (e.g., "smallest greater than", "largest less than or equal").
	/// </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///   A task that represents the asynchronous operation, containing the mapped LSN as a nullable byte array.
	/// </returns>
	Task<byte[]?> GetTimeToLsnAsync(DateTime lsnDate, string relationalOperator, CancellationToken cancellationToken);

	/// <summary>
	///   Retrieves the minimum LSN for a specific capture instance.
	/// </summary>
	/// <param name="captureInstance"> The capture instance to query. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the minimum LSN as a byte array. </returns>
	Task<byte[]> GetMinPositionAsync(string captureInstance, CancellationToken cancellationToken);

	/// <summary>
	///   Retrieves the maximum LSN currently available in the database.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the maximum LSN as a byte array. </returns>
	Task<byte[]> GetMaxPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	///   Retrieves the next valid LSN after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next valid LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///   A task that represents the asynchronous operation, containing the next valid LSN as a nullable byte array.
	/// </returns>
	Task<byte[]?> GetNextValidLsn(byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	///   Determines whether a given capture instance exists in the current database.
	/// </summary>
	/// <param name="captureInstance"> The capture instance to verify. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///   A task representing the asynchronous operation, containing <c> true </c> if the capture instance exists.
	/// </returns>
	Task<bool> CaptureInstanceExistsAsync(string captureInstance, CancellationToken cancellationToken);

	/// <summary>
	///   Retrieves the number of parameters required by the change function for a capture instance.
	/// </summary>
	/// <param name="captureInstance"> The capture instance whose change function parameters should be counted. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task representing the asynchronous operation, containing the parameter count. </returns>
	Task<int> GetChangeFunctionParameterCountAsync(string captureInstance, CancellationToken cancellationToken);

	/// <summary>
	///   Checks if changes exist between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="fromPosition"> The starting LSN position. </param>
	/// <param name="toPosition"> The ending LSN position. </param>
	/// <param name="captureInstances"> A collection of capture instance names to query for changes. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///   A task that represents the asynchronous operation, containing a boolean indicating whether changes exist.
	/// </returns>
	Task<bool> ChangesExistAsync(
		byte[] fromPosition,
		byte[] toPosition,
		IEnumerable<string> captureInstances,
		CancellationToken cancellationToken);

	/// <summary>
	///   Fetches change rows between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="captureInstance"> A capture instance table name to query for changes. </param>
	/// <param name="batchSize"> The number of records to retrieve in the batch. </param>
	/// <param name="lsn"> The LSN position. </param>
	/// <param name="lastSequenceValue">
	///   The last processed sequence value, if any. This is used for processing changes with finer granularity.
	/// </param>
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
