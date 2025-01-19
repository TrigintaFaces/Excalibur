namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the configuration settings for a database used in Change Data Capture (CDC) processing.
/// </summary>
public interface IDatabaseConfig
{
	/// <summary>
	///     Gets or sets the name of the database being processed.
	/// </summary>
	string DatabaseName { get; set; }

	/// <summary>
	///     Gets or sets the unique identifier for the database connection.
	/// </summary>
	/// <remarks> This identifier is used to differentiate between multiple database connections. </remarks>
	string DatabaseConnectionIdentifier { get; set; }

	/// <summary>
	///     Gets or sets the unique identifier for the connection to the state store database.
	/// </summary>
	/// <remarks> The state store database is used to persist CDC processing state. </remarks>
	string StateConnectionIdentifier { get; set; }

	/// <summary>
	///     Gets or sets a value indicating whether processing should stop when a table handler is missing.
	/// </summary>
	/// <remarks>
	///     If <c> true </c>, the processing will stop and throw an exception when a table does not have a registered handler. If <c> false
	///     </c>, processing will continue despite missing table handlers.
	/// </remarks>
	bool StopOnMissingTableHandler { get; set; }

	/// <summary>
	///     Gets or sets the list of CDC capture instances to process.
	/// </summary>
	/// <remarks> Each capture instance corresponds to a table or set of tables tracked by CDC in the database. </remarks>
	string[] CaptureInstances { get; set; }

	/// <summary>
	///     Gets or sets the time interval (in milliseconds) between processing batches of CDC changes.
	/// </summary>
	/// <remarks> This value determines how often the processor will poll for new changes. </remarks>
	int BatchTimeInterval { get; set; }
}
