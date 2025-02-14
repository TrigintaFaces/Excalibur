using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents a Change Data Capture (CDC) row retrieved from SQL Server.
/// </summary>
/// <remarks>
///     A CDC row contains metadata about the operation (e.g., insert, update, delete), the associated LSN (Log Sequence Number), and the
///     actual data changes.
/// </remarks>
public record CdcRow : IDisposable
{
	/// <summary>
	///     Gets or initializes the name of the table from which the CDC row originates.
	/// </summary>
	public required string TableName { get; init; }

	/// <summary>
	///     Gets or sets the Log Sequence Number (LSN) associated with the CDC row.
	/// </summary>
	/// <remarks> The LSN serves as a unique identifier for a transaction and its order in the CDC log. </remarks>
	public required byte[] Lsn { get; init; }

	/// <summary>
	///     Gets or sets the sequence value for the change.
	/// </summary>
	/// <remarks> The sequence value helps identify multiple changes within the same transaction. </remarks>
	public required byte[] SeqVal { get; init; }

	/// <summary>
	///     Gets or sets the operation code indicating the type of change.
	/// </summary>
	/// <remarks> Possible values are defined in the <see cref="CdcOperationCodes" /> enumeration. </remarks>
	public CdcOperationCodes OperationCode { get; init; }

	/// <summary>
	///     Gets or sets the commit time of the transaction that caused the change.
	/// </summary>
	public DateTime CommitTime { get; init; }

	private static readonly ConcurrentDictionary<string, ImmutableDictionary<string, Type?>> DataTypeCache = new();
	private static readonly ConcurrentBag<Dictionary<string, object>> ChangesPool = [];
	private static readonly ConcurrentBag<Dictionary<string, Type?>> DataTypesPool = [];

	/// <summary>
	///     Gets or sets a dictionary containing the actual data changes for the CDC row.
	/// </summary>
	/// <remarks> The dictionary contains column names as keys and their corresponding new values as values. </remarks>
	public required Dictionary<string, object> Changes { get; init; }

	/// <summary>
	///     Gets or sets a dictionary mapping column names to their data types.
	/// </summary>
	/// <remarks> This is useful for interpreting the data changes with their corresponding data types. </remarks>
	public required Dictionary<string, Type?> DataTypes { get; init; }

	public static ImmutableDictionary<string, Type?>? GetOrCreateDataTypes(string tableName, Action<Dictionary<string, Type?>> populateFunc)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentNullException.ThrowIfNull(populateFunc);

		return DataTypeCache.GetOrAdd(tableName, _ =>
		{
			var rentedDict = RentDataTypesDictionary();

			try
			{
				populateFunc(rentedDict);
				return rentedDict.ToImmutableDictionary();
			}
			finally
			{
				rentedDict.Clear();
				DataTypesPool.Add(rentedDict);
			}
		});
	}

	public static Dictionary<string, object> RentChangesDictionary()
	{
		if (!ChangesPool.TryTake(out var dict))
		{
			return [];
		}

		dict.Clear();
		return dict;
	}

	public static Dictionary<string, Type?> RentDataTypesDictionary()
	{
		if (!DataTypesPool.TryTake(out var dict))
		{
			return [];
		}

		dict.Clear();
		return dict;
	}

	private void ReleaseUnmanagedResources()
	{
		if (Changes.Count > 0)
		{
			Changes.Clear();
			ChangesPool.Add(Changes);
		}

		if (DataTypes.Count > 0)
		{
			DataTypes.Clear();
			DataTypesPool.Add(DataTypes);
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		ReleaseUnmanagedResources();
		if (disposing)
		{
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	~CdcRow() => Dispose(false);
}

public class CdcRowComparer : IComparer<CdcRow>
{
	public int Compare(CdcRow? x, CdcRow? y)
	{
		if (x == null || y == null)
		{
			return 0;
		}

		var tableComparison = string.Compare(x.TableName, y.TableName, StringComparison.Ordinal);
		if (tableComparison != 0)
		{
			return tableComparison;
		}

		var lsnComparison = x.Lsn.CompareLsn(y.Lsn);

		return lsnComparison != 0 ? lsnComparison : x.SeqVal.CompareLsn(y.SeqVal);
	}
}
