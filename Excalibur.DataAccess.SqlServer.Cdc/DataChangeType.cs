namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the type of data change captured during Change Data Capture (CDC) processing.
/// </summary>
public enum DataChangeType
{
	/// <summary>
	///     The data change type is unknown or not specified.
	/// </summary>
	Unknown = 0,

	/// <summary>
	///     The change represents an insert operation.
	/// </summary>
	Insert,

	/// <summary>
	///     The change represents an update operation.
	/// </summary>
	Update,

	/// <summary>
	///     The change represents a delete operation.
	/// </summary>
	Delete
}
