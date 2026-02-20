// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using NpgsqlTypes;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Represents a position in the Postgres Write-Ahead Log (WAL) for CDC tracking.
/// </summary>
/// <remarks>
/// <para>
/// Postgres uses Log Sequence Numbers (LSN) to track positions in the WAL.
/// An LSN is a 64-bit integer displayed as two hexadecimal numbers separated by a slash (e.g., "0/1234ABCD").
/// </para>
/// <para>
/// The high 32 bits represent the logical log file, and the low 32 bits represent the offset within that file.
/// </para>
/// </remarks>
public readonly struct PostgresCdcPosition : IEquatable<PostgresCdcPosition>, IComparable<PostgresCdcPosition>
{
	/// <summary>
	/// Represents the start of the WAL (position 0/0).
	/// </summary>
	public static readonly PostgresCdcPosition Start = new(NpgsqlLogSequenceNumber.Invalid);

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcPosition"/> struct.
	/// </summary>
	/// <param name="lsn">The log sequence number.</param>
	public PostgresCdcPosition(NpgsqlLogSequenceNumber lsn)
	{
		Lsn = lsn;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcPosition"/> struct from an LSN string.
	/// </summary>
	/// <param name="lsnString">The LSN string in the format "X/XXXXXXXX".</param>
	/// <exception cref="FormatException">Thrown when the string is not a valid LSN format.</exception>
	public PostgresCdcPosition(string lsnString)
	{
		if (string.IsNullOrWhiteSpace(lsnString))
		{
			Lsn = NpgsqlLogSequenceNumber.Invalid;
			return;
		}

		Lsn = NpgsqlLogSequenceNumber.Parse(lsnString);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcPosition"/> struct from a 64-bit value.
	/// </summary>
	/// <param name="lsnValue">The LSN as a 64-bit unsigned integer.</param>
	public PostgresCdcPosition(ulong lsnValue)
	{
		Lsn = new NpgsqlLogSequenceNumber(lsnValue);
	}

	/// <summary>
	/// Gets the underlying Npgsql log sequence number.
	/// </summary>
	public NpgsqlLogSequenceNumber Lsn { get; }

	/// <summary>
	/// Gets the LSN as a string in the format "X/XXXXXXXX".
	/// </summary>
	public string LsnString => Lsn.ToString();

	/// <summary>
	/// Gets the LSN as a 64-bit unsigned integer.
	/// </summary>
	public ulong LsnValue => (ulong)Lsn;

	/// <summary>
	/// Gets a value indicating whether this position is valid (not the start/invalid position).
	/// </summary>
	public bool IsValid => Lsn != NpgsqlLogSequenceNumber.Invalid;

	/// <summary>
	/// Parses an LSN string to a <see cref="PostgresCdcPosition"/>.
	/// </summary>
	/// <param name="lsnString">The LSN string to parse.</param>
	/// <returns>A new <see cref="PostgresCdcPosition"/>.</returns>
	/// <exception cref="FormatException">Thrown when the string is not a valid LSN format.</exception>
	public static PostgresCdcPosition Parse(string lsnString) => new(lsnString);

	/// <summary>
	/// Tries to parse an LSN string to a <see cref="PostgresCdcPosition"/>.
	/// </summary>
	/// <param name="lsnString">The LSN string to parse.</param>
	/// <param name="position">When successful, contains the parsed position.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryParse(string? lsnString, out PostgresCdcPosition position)
	{
		if (string.IsNullOrWhiteSpace(lsnString))
		{
			position = Start;
			return false;
		}

		if (NpgsqlLogSequenceNumber.TryParse(lsnString, out var lsn))
		{
			position = new PostgresCdcPosition(lsn);
			return true;
		}

		position = Start;
		return false;
	}

	/// <summary>
	/// Determines whether two positions are equal.
	/// </summary>
	public static bool operator ==(PostgresCdcPosition left, PostgresCdcPosition right) => left.Equals(right);

	/// <summary>
	/// Determines whether two positions are not equal.
	/// </summary>
	public static bool operator !=(PostgresCdcPosition left, PostgresCdcPosition right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the left position is less than the right position.
	/// </summary>
	public static bool operator <(PostgresCdcPosition left, PostgresCdcPosition right) => left.CompareTo(right) < 0;

	/// <summary>
	/// Determines whether the left position is greater than the right position.
	/// </summary>
	public static bool operator >(PostgresCdcPosition left, PostgresCdcPosition right) => left.CompareTo(right) > 0;

	/// <summary>
	/// Determines whether the left position is less than or equal to the right position.
	/// </summary>
	public static bool operator <=(PostgresCdcPosition left, PostgresCdcPosition right) => left.CompareTo(right) <= 0;

	/// <summary>
	/// Determines whether the left position is greater than or equal to the right position.
	/// </summary>
	public static bool operator >=(PostgresCdcPosition left, PostgresCdcPosition right) => left.CompareTo(right) >= 0;

	/// <inheritdoc/>
	public bool Equals(PostgresCdcPosition other) => Lsn == other.Lsn;

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is PostgresCdcPosition other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => Lsn.GetHashCode();

	/// <inheritdoc/>
	public int CompareTo(PostgresCdcPosition other) => Lsn.CompareTo(other.Lsn);

	/// <summary>
	/// Converts this position to a <see cref="ChangePosition"/> for use with the unified
	/// <see cref="ICdcStateStore"/> contract.
	/// </summary>
	/// <returns>A <see cref="TokenChangePosition"/> representing this LSN position.</returns>
	public ChangePosition ToChangePosition() =>
		IsValid ? new TokenChangePosition(LsnString) : TokenChangePosition.Empty;

	/// <summary>
	/// Creates a <see cref="PostgresCdcPosition"/> from a <see cref="ChangePosition"/>.
	/// </summary>
	/// <param name="position">The change position to convert.</param>
	/// <returns>A <see cref="PostgresCdcPosition"/> parsed from the token.</returns>
	public static PostgresCdcPosition FromChangePosition(ChangePosition? position)
	{
		if (position is not null && position.IsValid && TryParse(position.ToToken(), out var result))
		{
			return result;
		}

		return Start;
	}

	/// <inheritdoc/>
	public override string ToString() => LsnString;
}
