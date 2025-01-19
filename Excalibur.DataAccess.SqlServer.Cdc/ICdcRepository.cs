namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines the contract for a repository that interacts with Change Data Capture (CDC) metadata and data.
/// </summary>
public interface ICdcRepository
{
	/// <summary>
	///     Retrieves the next Log Sequence Number (LSN) after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the next LSN as a byte array. </returns>
	Task<byte[]> GetNextLsn(byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	///     Maps an LSN to a commit time in the database.
	/// </summary>
	/// <param name="lsn"> The LSN to map to a time. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the mapped commit time as a nullable <see cref="DateTime" />. </returns>
	Task<DateTime?> GetLsnToTime(byte[] lsn, CancellationToken cancellationToken);

	/// <summary>
	///     Maps a time to an LSN using the specified relational operator.
	/// </summary>
	/// <param name="lsnDate"> The date to map to an LSN. </param>
	/// <param name="relationalOperator">
	///     The relational operator for mapping (e.g., "smallest greater than", "largest less than or equal").
	/// </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the mapped LSN as a nullable byte array. </returns>
	Task<byte[]?> GetTimeToLsn(DateTime lsnDate, string relationalOperator, CancellationToken cancellationToken);

	/// <summary>
	///     Retrieves the minimum LSN for a specific capture instance.
	/// </summary>
	/// <param name="captureInstance"> The capture instance to query. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the minimum LSN as a byte array. </returns>
	Task<byte[]> GetMinPositionAsync(string captureInstance, CancellationToken cancellationToken);

	/// <summary>
	///     Retrieves the maximum LSN currently available in the database.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the maximum LSN as a byte array. </returns>
	Task<byte[]> GetMaxPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	///     Retrieves the next valid LSN after the specified last processed LSN.
	/// </summary>
	/// <param name="lastProcessedLsn"> The LSN from which to find the next valid LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing the next valid LSN as a nullable byte array. </returns>
	Task<byte[]?> GetNextValidLsn(byte[] lastProcessedLsn, CancellationToken cancellationToken);

	/// <summary>
	///     Checks if changes exist between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="fromPosition"> The starting LSN position. </param>
	/// <param name="toPosition"> The ending LSN position. </param>
	/// <param name="captureInstances"> A collection of capture instance names to query for changes. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation, containing a boolean indicating whether changes exist. </returns>
	Task<bool> ChangesExistAsync(byte[] fromPosition, byte[] toPosition, IEnumerable<string> captureInstances,
		CancellationToken cancellationToken);

	/// <summary>
	///     Fetches change rows between two LSN positions for the specified capture instances.
	/// </summary>
	/// <param name="fromPosition"> The starting LSN position. </param>
	/// <param name="toPosition"> The ending LSN position. </param>
	/// <param name="lastSequenceValue">
	///     The last processed sequence value, if any. This is used for processing changes with finer granularity.
	/// </param>
	/// <param name="captureInstances"> A collection of capture instance names to query for changes. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> An asynchronous stream of <see cref="CdcRow" /> instances representing the captured changes. </returns>
	IAsyncEnumerable<CdcRow> FetchChangesAsync(byte[] fromPosition, byte[] toPosition, byte[]? lastSequenceValue,
		IEnumerable<string> captureInstances, CancellationToken cancellationToken);
}
