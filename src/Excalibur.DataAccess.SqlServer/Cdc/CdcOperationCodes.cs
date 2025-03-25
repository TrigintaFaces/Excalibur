namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Enumeration representing the types of Change Data Capture (CDC) operations.
/// </summary>
public enum CdcOperationCodes
{
	/// <summary>
	///     Represents an unknown operation. This is the default value.
	/// </summary>
	Unknown = 0,

	/// <summary>
	///     Represents a delete operation, indicating that a row was removed from the table.
	/// </summary>
	Delete,

	/// <summary>
	///     Represents an insert operation, indicating that a new row was added to the table.
	/// </summary>
	Insert,

	/// <summary>
	///     Represents the state of a row before an update operation.
	/// </summary>
	UpdateBefore,

	/// <summary>
	///     Represents the state of a row after an update operation.
	/// </summary>
	UpdateAfter,
}
