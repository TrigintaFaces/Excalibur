// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Compares byte arrays lexicographically (byte-by-byte). Used for ordering LSN values.
/// </summary>
public sealed class ByteArrayComparer : IComparer<byte[]>, System.Collections.IComparer
{
	/// <summary>
	/// Compares two byte arrays lexicographically.
	/// </summary>
	/// <param name="x">The first byte array to compare.</param>
	/// <param name="y">The second byte array to compare.</param>
	/// <returns>
	/// A negative value if x is less than y, zero if they are equal,
	/// or a positive value if x is greater than y.
	/// </returns>
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

		if (x is byte[] a
			&& y is byte[] b)
		{
			return Compare(a, b);
		}

		throw new ArgumentException("", nameof(x));
	}
}
