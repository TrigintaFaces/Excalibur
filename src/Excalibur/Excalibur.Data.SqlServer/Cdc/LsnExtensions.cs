// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Extension methods for working with SQL Server Change Data Capture LSN (Log Sequence Number) values.
/// </summary>
public static class LsnExtensions
{
	private static readonly ByteArrayComparer ByteArrayComparer = new();

	/// <summary>
	/// Compares two LSN values for ordering.
	/// </summary>
	/// <param name="lsn1"> The first LSN value. </param>
	/// <param name="lsn2"> The second LSN value. </param>
	/// <returns>
	/// A negative value if lsn1 is less than lsn2, zero if they are equal, or a positive value if lsn1 is greater than lsn2.
	/// </returns>
	public static int CompareLsn(this byte[] lsn1, byte[] lsn2) => ByteArrayComparer.Compare(lsn1, lsn2);

	/// <summary>
	/// Converts a byte array to a hexadecimal string representation.
	/// </summary>
	/// <param name="bytes"> The byte array to convert. </param>
	/// <returns> A hexadecimal string representation of the byte array. </returns>
	public static string ByteArrayToHex(this byte[] bytes) => $"0x{Convert.ToHexString(bytes)}";
}
