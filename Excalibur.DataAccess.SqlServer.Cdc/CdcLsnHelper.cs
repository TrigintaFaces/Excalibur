namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class CdcLsnHelper
{
	private static readonly ByteArrayComparer ByteArrayComparer = new();

	public static int CompareLsn(this byte[] lsn1, byte[] lsn2) => ByteArrayComparer.Compare(lsn1, lsn2);
}

public class MinHeapComparer : IComparer<(byte[] Lsn, string TableName)>
{
	private readonly ByteArrayComparer _byteArrayComparer = new();

	public int Compare((byte[] Lsn, string TableName) x, (byte[] Lsn, string TableName) y)
	{
		var lsnComparison = _byteArrayComparer.Compare(x.Lsn, y.Lsn);

		return lsnComparison != 0 ? lsnComparison : string.Compare(x.TableName, y.TableName, StringComparison.Ordinal);
	}
}

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
