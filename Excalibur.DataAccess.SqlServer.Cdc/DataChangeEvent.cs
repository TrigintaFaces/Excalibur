namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents a data change event in a database table, including details about the change type, affected table, and data changes.
/// </summary>
public class DataChangeEvent
{
	/// <summary>
	///     Gets the log sequence number (LSN) associated with the change event.
	/// </summary>
	public byte[] Lsn { get; init; }

	/// <summary>
	///     Gets the name of the table where the change occurred.
	/// </summary>
	public string TableName { get; init; }

	/// <summary>
	///     Gets the type of the data change (e.g., Insert, Update, Delete).
	/// </summary>
	public DataChangeType ChangeType { get; init; }

	/// <summary>
	///     Gets the collection of individual data changes for the affected table row.
	/// </summary>
	public IEnumerable<DataChange> Changes { get; init; }

	/// <summary>
	///     Creates a <see cref="DataChangeEvent" /> for a delete operation.
	/// </summary>
	/// <param name="change"> The CDC row representing the delete operation. </param>
	/// <returns> A <see cref="DataChangeEvent" /> representing the delete operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="change" /> is null. </exception>
	public static DataChangeEvent CreateDeleteEvent(CdcRow change)
	{
		ArgumentNullException.ThrowIfNull(change);

		return new DataChangeEvent
		{
			Lsn = change.Lsn,
			TableName = change.TableName,
			ChangeType = DataChangeType.Delete,
			Changes = change.Changes.Select(data => new DataChange
			{
				ColumnName = data.Key,
				OldValue = data.Value != DBNull.Value ? data.Value : null,
				NewValue = null,
				DataType = change.DataTypes[data.Key]
			}).ToList()
		};
	}

	/// <summary>
	///     Creates a <see cref="DataChangeEvent" /> for an insert operation.
	/// </summary>
	/// <param name="change"> The CDC row representing the insert operation. </param>
	/// <returns> A <see cref="DataChangeEvent" /> representing the insert operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="change" /> is null. </exception>
	public static DataChangeEvent CreateInsertEvent(CdcRow change)
	{
		ArgumentNullException.ThrowIfNull(change);

		return new DataChangeEvent
		{
			Lsn = change.Lsn,
			TableName = change.TableName,
			ChangeType = DataChangeType.Insert,
			Changes = change.Changes.Select(data => new DataChange
			{
				ColumnName = data.Key,
				OldValue = null,
				NewValue = data.Value != DBNull.Value ? data.Value : null,
				DataType = change.DataTypes[data.Key]
			}).ToList()
		};
	}

	/// <summary>
	///     Creates a <see cref="DataChangeEvent" /> for an update operation.
	/// </summary>
	/// <param name="beforeChange"> The CDC row representing the state before the update. </param>
	/// <param name="afterChange"> The CDC row representing the state after the update. </param>
	/// <returns> A <see cref="DataChangeEvent" /> representing the update operation. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if either <paramref name="beforeChange" /> or <paramref name="afterChange" /> is null.
	/// </exception>
	public static DataChangeEvent CreateUpdateEvent(CdcRow beforeChange, CdcRow afterChange)
	{
		ArgumentNullException.ThrowIfNull(beforeChange);
		ArgumentNullException.ThrowIfNull(afterChange);

		var dataChanges = beforeChange.Changes.Select(data =>
		{
			var columnName = data.Key;
			var dataType = beforeChange.DataTypes[columnName];
			var oldValue = data.Value != DBNull.Value ? data.Value : null;
			var newValue = afterChange.Changes.TryGetValue(columnName, out var newVal) && newVal != DBNull.Value ? newVal : null;
			return new DataChange { ColumnName = columnName, OldValue = oldValue, NewValue = newValue, DataType = dataType };
		});

		return new DataChangeEvent
		{
			Lsn = beforeChange.Lsn,
			TableName = beforeChange.TableName,
			ChangeType = DataChangeType.Update,
			Changes = dataChanges.ToList()
		};
	}
}
