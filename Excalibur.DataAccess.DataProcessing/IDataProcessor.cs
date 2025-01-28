namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Defines a contract for processing data tasks.
/// </summary>
public interface IDataProcessor : IDisposable
{
	/// <summary>
	///     Executes the data processing pipeline.
	/// </summary>
	/// <param name="completedCount"> The count of records that have already been processed, used to resume from a specific point. </param>
	/// <param name="updateCompletedCount"> A delegate for updating the count of completed records in the data task. </param>
	/// <param name="cancellationToken"> A token to signal the cancellation of the processing operation. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken);
}
