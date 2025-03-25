namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the state of CDC (Change Data Capture) processing, including the last processed log sequence number (LSN), sequence
///     value, and other metadata.
/// </summary>
public class CdcProcessingState
{
	/// <summary>
	///     Gets or sets the last processed Log Sequence Number (LSN) in the CDC process.
	/// </summary>
	/// <remarks>
	///     The LSN is a unique identifier for a change operation in the database. This is used to track the progress of CDC processing.
	/// </remarks>
	public byte[] LastProcessedLsn { get; init; } = new byte[10];

	/// <summary>
	///     Gets or sets the last processed sequence value in the CDC process.
	/// </summary>
	/// <remarks> This value may be used to track sequence changes within a batch or transaction, if applicable. </remarks>
	public byte[]? LastProcessedSequenceValue { get; init; }

	/// <summary>
	///     Gets or sets the timestamp of the last commit operation that was processed.
	/// </summary>
	/// <remarks> This value indicates the latest commit time for the data captured by the CDC process. </remarks>
	public DateTime LastCommitTime { get; init; }

	/// <summary>
	///     Gets or sets the timestamp when the CDC processing state was updated.
	/// </summary>
	/// <remarks> This value reflects the time at which the CDC processing state was last persisted or recorded. </remarks>
	public DateTimeOffset ProcessedAt { get; init; }

	/// <summary>
	///     Gets or sets the identifier for the database connection used in the CDC process.
	/// </summary>
	/// <remarks>
	///     This value uniquely identifies the database connection, which can be useful for scenarios where multiple connections are used in
	///     the CDC process.
	/// </remarks>
	public string DatabaseConnectionIdentifier { get; init; }

	/// <summary>
	///     Gets or sets the name of the database being processed.
	/// </summary>
	/// <remarks> This property identifies the database for which CDC is being tracked. </remarks>
	public string DatabaseName { get; init; }

	/// <summary>
	///     Gets or sets the name of the table being processed.
	/// </summary>
	/// <remarks> This property identifies the table for which CDC is being tracked. </remarks>
	public string TableName { get; init; }
}
