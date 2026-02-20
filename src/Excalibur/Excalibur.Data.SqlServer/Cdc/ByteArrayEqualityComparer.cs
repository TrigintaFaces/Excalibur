// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Compares byte arrays for equality and provides a consistent hash code implementation. Intended for dictionary key comparisons where byte
/// arrays represent LSNs or sequence values.
/// </summary>
public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>, System.Collections.IEqualityComparer
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
		ArgumentNullException.ThrowIfNull(obj);

		unchecked
		{
			return obj.Aggregate(17, static (current, b) => (current * 31) + b.GetHashCode());
		}
	}
	public new bool Equals(object? x, object? y)
	{
		if (x == y)
		{
			return true;
		}

		if (x == null || y == null)
		{
			return false;
		}

		if (x is byte[] a
			&& y is byte[] b)
		{
			return Equals(a, b);
		}

		throw new ArgumentException("", nameof(x));
	}
	public int GetHashCode(object obj)
	{
		if (obj == null)
		{
			return 0;
		}

		if (obj is byte[] x)
		{
			return GetHashCode(x);
		}

		throw new ArgumentException("", nameof(obj));
	}
}
