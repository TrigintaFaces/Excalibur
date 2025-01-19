namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines the contract for a Change Data Capture (CDC) processor, which processes CDC changes and delegates handling to a specified
///     event handler.
/// </summary>
public interface ICdcProcessor
{
	/// <summary>
	///     Processes CDC changes and invokes the specified event handler for each batch of data change events.
	/// </summary>
	/// <param name="eventHandler">
	///     A delegate that handles an array of <see cref="DataChangeEvent" /> instances. The delegate should return the number of events
	///     successfully processed.
	/// </param>
	/// <param name="cancellationToken">
	///     A token to observe while waiting for the task to complete. This token can be used to cancel the processing operation.
	/// </param>
	/// <returns>
	///     A task representing the asynchronous operation. The task result contains the total number of events processed successfully.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="eventHandler" /> is null. </exception>
	/// <exception cref="OperationCanceledException"> Thrown if the operation is canceled via the <paramref name="cancellationToken" />. </exception>
	Task<int> ProcessCdcChangesAsync(Func<DataChangeEvent[], CancellationToken, Task<int>> eventHandler,
		CancellationToken cancellationToken);
}
