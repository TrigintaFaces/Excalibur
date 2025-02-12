namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines the contract for managing the state of Change Data Capture (CDC) processing.
/// </summary>
public interface ICdcStateStore
{
	/// <summary>
	///     Retrieves the last processed position for a specified database connection and name.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The unique identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database to retrieve the processing state for. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>
	///     A task that represents the asynchronous operation, containing a collection of the last processed
	///     <see cref="CdcProcessingState" /> for each capture instance.
	/// </returns>
	Task<IEnumerable<CdcProcessingState>> GetLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		CancellationToken cancellationToken);

	/// <summary>
	///     Updates the last processed position for a specified database connection and name.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The unique identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database to update the processing state for. </param>
	/// <param name="tableName"> The name of the table to update the processing state for. </param>
	/// <param name="position"> The LSN position to set as the last processed position. </param>
	/// <param name="sequenceValue"> The sequence value associated with the last processed position, if any. </param>
	/// <param name="commitTime"> The commit time of the last processed LSN. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	Task UpdateLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		string tableName,
		byte[] position,
		byte[]? sequenceValue,
		DateTime commitTime,
		CancellationToken cancellationToken);
}
