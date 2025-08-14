namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines the interface for processing Change Data Capture (CDC) events into data change events.
/// </summary>
public interface IDataChangeEventProcessor : ICdcProcessor
{
	/// <summary>
	///     Processes CDC changes by transforming them into data change events and delegating to appropriate handlers.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The total number of data change events processed. </returns>
	/// <remarks>
	///     This method retrieves CDC changes, converts them into <see cref="DataChangeEvent" /> instances, and invokes the appropriate
	///     handler for each event based on the table involved.
	/// </remarks>
	public Task<int> ProcessCdcChangesAsync(CancellationToken cancellationToken);
}
