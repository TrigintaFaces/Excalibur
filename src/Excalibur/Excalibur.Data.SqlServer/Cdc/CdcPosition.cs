// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Represents the current position in the CDC log for a specific table,
/// defined by a Log Sequence Number (LSN) and an optional sequence value.
/// </summary>
/// <remarks>
/// <para>
/// This type bridges the SQL Server-specific LSN/SeqVal position representation with the
/// unified <see cref="ChangePosition"/> abstraction via <see cref="ToChangePosition"/> and
/// <see cref="FromChangePosition"/>. This follows the same adapter pattern used by
/// <c>PostgresCdcPosition</c> and <c>MongoDbCdcPosition</c>.
/// </para>
/// <para>
/// SQL Server CDC positions consist of a 10-byte Log Sequence Number and an optional
/// sequence value for sub-LSN ordering within the same transaction.
/// </para>
/// </remarks>
public sealed class CdcPosition(byte[] lsn, byte[]? seqVal) : IEquatable<CdcPosition>
{
	/// <summary>
	/// Gets the Log Sequence Number indicating the current CDC position.
	/// </summary>
	/// <value> The Log Sequence Number indicating the current CDC position. </value>
	public byte[] Lsn { get; init; } = lsn ?? throw new ArgumentNullException(nameof(lsn));

	/// <summary>
	/// Gets an optional sequence value for finer-grained CDC tracking.
	/// </summary>
	/// <value> An optional sequence value for finer-grained CDC tracking. </value>
	public byte[]? SequenceValue { get; init; } = seqVal;

	/// <summary>
	/// Gets a value indicating whether this position is valid (has a non-empty LSN).
	/// </summary>
	public bool IsValid => Lsn.Length > 0;

	/// <summary>
	/// Converts this position to a <see cref="ChangePosition"/> for use with the unified
	/// <see cref="Excalibur.Dispatch.Abstractions.ICdcStateStore"/> contract.
	/// </summary>
	/// <returns>
	/// A <see cref="TokenChangePosition"/> with the LSN encoded as a hex string,
	/// optionally including the sequence value separated by a pipe character.
	/// </returns>
	public ChangePosition ToChangePosition()
	{
		if (!IsValid)
		{
			return TokenChangePosition.Empty;
		}

		var token = Convert.ToHexString(Lsn);
		if (SequenceValue is not null)
		{
			token += "|" + Convert.ToHexString(SequenceValue);
		}

		return new TokenChangePosition(token);
	}

	/// <summary>
	/// Creates a <see cref="CdcPosition"/> from a <see cref="ChangePosition"/>.
	/// </summary>
	/// <param name="position">The change position to convert.</param>
	/// <returns>
	/// A <see cref="CdcPosition"/> parsed from the token, or a position with an empty LSN
	/// if the input is null, invalid, or cannot be parsed.
	/// </returns>
	public static CdcPosition FromChangePosition(ChangePosition? position)
	{
		if (position is null || !position.IsValid)
		{
			return new CdcPosition([], null);
		}

		var token = position.ToToken();
		var pipeIndex = token.IndexOf('|', StringComparison.Ordinal);

		if (pipeIndex < 0)
		{
			return new CdcPosition(Convert.FromHexString(token), null);
		}

		var lsnHex = token[..pipeIndex];
		var seqHex = token[(pipeIndex + 1)..];
		return new CdcPosition(Convert.FromHexString(lsnHex), Convert.FromHexString(seqHex));
	}

	/// <inheritdoc/>
	public bool Equals(CdcPosition? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return Lsn.AsSpan().SequenceEqual(other.Lsn.AsSpan())
			   && (SequenceValue is null
				   ? other.SequenceValue is null
				   : other.SequenceValue is not null && SequenceValue.AsSpan().SequenceEqual(other.SequenceValue.AsSpan()));
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is CdcPosition other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var b in Lsn)
		{
			hash.Add(b);
		}

		if (SequenceValue is not null)
		{
			foreach (var b in SequenceValue)
			{
				hash.Add(b);
			}
		}

		return hash.ToHashCode();
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		var lsnHex = Lsn.Length > 0 ? $"0x{Convert.ToHexString(Lsn)}" : "(empty)";
		var seqHex = SequenceValue is not null ? $", SeqVal=0x{Convert.ToHexString(SequenceValue)}" : string.Empty;
		return $"CdcPosition(LSN={lsnHex}{seqHex})";
	}
}
