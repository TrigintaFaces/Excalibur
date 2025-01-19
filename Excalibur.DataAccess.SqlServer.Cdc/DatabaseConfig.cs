using System.ComponentModel.DataAnnotations;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the configuration settings required for CDC (Change Data Capture) processing.
/// </summary>
public class DatabaseConfig : IDatabaseConfig
{
	/// <summary>
	///     Gets or sets the name of the database to monitor for changes.
	/// </summary>
	/// <remarks> This value identifies the target database where CDC is enabled. </remarks>
	[Required]
	public string DatabaseName { get; set; }

	/// <summary>
	///     Gets or sets the unique identifier for the database connection.
	/// </summary>
	/// <remarks> This value is used to differentiate connections in multi-database scenarios. </remarks>
	[Required]
	public string DatabaseConnectionIdentifier { get; set; }

	/// <summary>
	///     Gets or sets the unique identifier for the state store connection.
	/// </summary>
	/// <remarks> This is used to connect to the state storage where CDC processing metadata is tracked. </remarks>
	[Required]
	public string StateConnectionIdentifier { get; set; }

	/// <summary>
	///     Gets or sets a value indicating whether processing should stop when no handler is found for a capture table.
	/// </summary>
	/// <remarks> If set to <c> true </c>, CDC processing will halt if a table handler is missing, ensuring data integrity. </remarks>
	[Required]
	public bool StopOnMissingTableHandler { get; set; }

	/// <summary>
	///     Gets or sets the list of capture instances to monitor for changes.
	/// </summary>
	/// <remarks> Capture instances represent the CDC-enabled tables in the database. </remarks>
	[Required]
	public string[] CaptureInstances { get; set; }

	/// <summary>
	///     Gets or sets the batch time interval (in milliseconds) for processing changes.
	/// </summary>
	/// <remarks> This value determines the maximum duration between batch queries to the database. </remarks>
	[Required]
	public int BatchTimeInterval { get; set; }
}
