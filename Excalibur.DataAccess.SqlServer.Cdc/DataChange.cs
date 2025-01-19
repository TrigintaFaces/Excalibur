namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents a change to a specific column in a database row, capturing the old and new values.
/// </summary>
public class DataChange
{
	/// <summary>
	///     Gets the name of the column where the change occurred.
	/// </summary>
	/// <remarks> This value represents the name of the affected database column. </remarks>
	public string ColumnName { get; init; }

	/// <summary>
	///     Gets the value of the column before the change occurred.
	/// </summary>
	/// <remarks> This property may be <c> null </c> if the column did not have a previous value (e.g., during an insert operation). </remarks>
	public object? OldValue { get; init; }

	/// <summary>
	///     Gets the value of the column after the change occurred.
	/// </summary>
	/// <remarks> This property may be <c> null </c> if the column does not have a new value (e.g., during a delete operation). </remarks>
	public object? NewValue { get; init; }

	/// <summary>
	///     Gets the data type of the column.
	/// </summary>
	/// <remarks> This value provides additional metadata about the column's type (e.g., <see cref="int" />, <see cref="string" />). </remarks>
	public Type? DataType { get; init; }

	/// <summary>
	///     Returns a string representation of the <see cref="DataChange" /> instance.
	/// </summary>
	/// <returns> A string showing the column name, old value, new value, and data type. </returns>
	public override string ToString() => $"{ColumnName}: {OldValue} → {NewValue} (Type: {DataType?.Name ?? "Unknown"})";
}
