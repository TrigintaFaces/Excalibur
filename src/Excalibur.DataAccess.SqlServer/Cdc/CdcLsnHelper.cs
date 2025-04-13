namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class CdcLsnHelper
{
	private static readonly ByteArrayComparer ByteArrayComparer = new();

	public static int CompareLsn(this byte[] lsn1, byte[] lsn2) => ByteArrayComparer.Compare(lsn1, lsn2);

	/// <summary>
	///     Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	public static string ByteArrayToHex(this byte[] bytes) => $"0x{Convert.ToHexString(bytes)}";
}

/// <summary>
///     Compares tuples of LSN and table name for sorting in a min-heap. Orders by ascending LSN; if equal, orders lexically by table name.
/// </summary>
public class MinHeapComparer : IComparer<(byte[] Lsn, string TableName)>
{
	private readonly ByteArrayComparer _byteArrayComparer = new();

	public int Compare((byte[] Lsn, string TableName) x, (byte[] Lsn, string TableName) y)
	{
		var lsnComparison = _byteArrayComparer.Compare(x.Lsn, y.Lsn);

		return lsnComparison != 0 ? lsnComparison : string.Compare(x.TableName, y.TableName, StringComparison.Ordinal);
	}
}

/// <summary>
///     Compares byte arrays lexicographically (byte-by-byte). Used for ordering LSN values.
/// </summary>
public class ByteArrayComparer : IComparer<byte[]>
{
	public int Compare(byte[]? x, byte[]? y)
	{
		if (ReferenceEquals(x, y))
		{
			return 0;
		}

		if (x == null)
		{
			return -1;
		}

		if (y == null)
		{
			return 1;
		}

		var length = Math.Min(x.Length, y.Length);
		for (var i = 0; i < length; i++)
		{
			var comparison = x[i].CompareTo(y[i]);
			if (comparison != 0)
			{
				return comparison;
			}
		}

		return x.Length.CompareTo(y.Length);
	}
}

/// <summary>
///     Compares byte arrays for equality and provides a consistent hash code implementation. Intended for dictionary key comparisons where
///     byte arrays represent LSNs or sequence values.
/// </summary>
public class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
{
	/// <inheritdoc />
	public bool Equals(byte[]? x, byte[]? y)
	{
		if (x == null || y == null)
		{
			return x == y;
		}

		return x.SequenceEqual(y);
	}

	/// <inheritdoc />
	public int GetHashCode(byte[] obj)
	{
		ArgumentNullException.ThrowIfNull(obj, nameof(obj));

		unchecked
		{
			return obj.Aggregate(17, (int current, byte b) => current * 31 + b.GetHashCode());
		}
	}
}
