namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines a handler for processing data change events for specific database tables.
/// </summary>
public interface IDataChangeHandler
{
	/// <summary>
	///     Gets the names of the tables that this handler is responsible for processing.
	/// </summary>
	/// <remarks>
	///     Each table name corresponds to a database table that the handler can process. Table names should match the names in the CDC
	///     configuration exactly.
	/// </remarks>
	string[] TableNames { get; }

	/// <summary>
	///     Processes a data change event for a specific table.
	/// </summary>
	/// <param name="changeEvent"> The <see cref="DataChangeEvent" /> representing the data change to process. </param>
	/// <param name="cancellationToken">
	///     A token to monitor for cancellation requests. This should be observed to cancel long-running operations in a timely manner.
	/// </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="changeEvent" /> is <c> null </c>. </exception>
	/// <exception cref="Exception">
	///     Any unexpected error during the processing of the change event should be logged and, if necessary, handled by higher-level components.
	/// </exception>
	Task Handle(DataChangeEvent changeEvent, CancellationToken cancellationToken);
}
