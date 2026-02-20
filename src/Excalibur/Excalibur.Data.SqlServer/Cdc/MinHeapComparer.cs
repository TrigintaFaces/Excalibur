// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Compares tuples of LSN and table name for sorting in a min-heap. Orders by ascending LSN; if equal, orders lexically by table name.
/// </summary>
public sealed class MinHeapComparer : IComparer<(byte[] Lsn, string TableName)>, System.Collections.IComparer
{
	private readonly ByteArrayComparer _byteArrayComparer = new();

	/// <summary>
	/// Compares two tuples containing LSN and table name pairs for ordering in a min-heap structure.
	/// </summary>
	/// <param name="x">The first tuple to compare, containing an LSN byte array and table name.</param>
	/// <param name="y">The second tuple to compare, containing an LSN byte array and table name.</param>
	/// <returns>
	/// A signed integer that indicates the relative values of x and y:
	/// Less than zero if x is less than y (x should come before y in sort order);
	/// Zero if x equals y;
	/// Greater than zero if x is greater than y (x should come after y in sort order).
	/// Comparison is performed first by LSN in ascending order, then by table name lexically if LSNs are equal.
	/// </returns>
	public int Compare((byte[] Lsn, string TableName) x, (byte[] Lsn, string TableName) y)
	{
		var lsnComparison = _byteArrayComparer.Compare(x.Lsn, y.Lsn);

		return lsnComparison != 0 ? lsnComparison : string.CompareOrdinal(x.TableName, y.TableName);
	}
	public int Compare(object? x, object? y)
	{
		if (x == y)
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

		if (x is ValueTuple<byte[], string> a && y is ValueTuple<byte[], string> b)
		{
			return Compare(a, b);
		}

		throw new ArgumentException("", nameof(x));
	}
}
