namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Defines methods for managing and processing data tasks for different record types.
/// </summary>
public interface IDataOrchestrationManager
{
	/// <summary>
	///     Adds a new data task for a specific record type.
	/// </summary>
	/// <param name="recordType"> The type of record for which the data task is created. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A <see cref="Guid" /> representing the unique identifier of the created data task. </returns>
	public Task<Guid> AddDataTaskForRecordType(string recordType, CancellationToken cancellationToken = default);

	/// <summary>
	///     Processes all pending data tasks.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public ValueTask ProcessDataTasks(CancellationToken cancellationToken = default);
}
