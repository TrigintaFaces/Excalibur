namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the configuration settings for a database used in Change Data Capture (CDC) processing.
/// </summary>
public interface IDatabaseConfig
{
	/// <summary>
	///     Gets the name of the database being processed.
	/// </summary>
	public string DatabaseName { get; }

	/// <summary>
	///     Gets the unique identifier for the database connection.
	/// </summary>
	/// <remarks> This identifier is used to differentiate between multiple database connections. </remarks>
	public string DatabaseConnectionIdentifier { get; }

	/// <summary>
	///     Gets the unique identifier for the connection to the state store database.
	/// </summary>
	/// <remarks> The state store database is used to persist CDC processing state. </remarks>
	public string StateConnectionIdentifier { get; }

	/// <summary>
	///     Gets a value indicating whether processing should stop when a table handler is missing.
	/// </summary>
	/// <remarks>
	///     If <c> true </c>, the processing will stop and throw an exception when a table does not have a registered handler. If <c> false
	///     </c>, processing will continue despite missing table handlers.
	/// </remarks>
	public bool StopOnMissingTableHandler { get; }

	/// <summary>
	///     Gets the list of CDC capture instances to process.
	/// </summary>
	/// <remarks> Each capture instance corresponds to a table or set of tables tracked by CDC in the database. </remarks>
	public string[] CaptureInstances { get; }

	/// <summary>
	///     Gets the batch time interval (in milliseconds) for processing changes.
	/// </summary>
	public int BatchTimeInterval { get; }

	/// <summary>
	///     Gets the size of the in-memory data queue.
	/// </summary>
	public int QueueSize { get; }

	/// <summary>
	///     Gets the batch size used in the producer loop.
	/// </summary>
	public int ProducerBatchSize { get; }

	/// <summary>
	///     Gets the batch size used in the consumer loop.
	/// </summary>
	public int ConsumerBatchSize { get; }
}
