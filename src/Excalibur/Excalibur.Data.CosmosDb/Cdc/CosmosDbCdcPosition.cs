// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Represents a position in the CosmosDb Change Feed using continuation tokens.
/// </summary>
/// <remarks>
/// <para>
/// CosmosDb continuation tokens are opaque strings (base64-encoded JSON) that encode
/// the partition state for resuming Change Feed processing.
/// </para>
/// <para>
/// Continuation tokens can be large (1KB+) and may become invalid if the container
/// is deleted and recreated.
/// </para>
/// </remarks>
public sealed class CosmosDbCdcPosition : ChangePosition, IEquatable<CosmosDbCdcPosition>
{
	private CosmosDbCdcPosition(string? continuationToken, DateTimeOffset? timestamp)
	{
		ContinuationToken = continuationToken;
		Timestamp = timestamp;
	}

	/// <summary>
	/// Gets the continuation token for resuming the Change Feed.
	/// </summary>
	public string? ContinuationToken { get; }

	/// <summary>
	/// Gets the timestamp from which to start reading (if no continuation token).
	/// </summary>
	public override DateTimeOffset? Timestamp { get; }

	/// <summary>
	/// Gets a value indicating whether this position is valid for resuming.
	/// </summary>
	public override bool IsValid => ContinuationToken is not null;

	/// <summary>
	/// Gets a value indicating whether this position represents the beginning of the feed.
	/// </summary>
	public bool IsBeginning => ContinuationToken is null && Timestamp is null;

	/// <summary>
	/// Creates a position representing the beginning of the Change Feed.
	/// </summary>
	/// <returns>A position that will read from the beginning.</returns>
	public static CosmosDbCdcPosition Beginning() => new(null, null);

	/// <summary>
	/// Creates a position representing the current time (reads only new changes).
	/// </summary>
	/// <returns>A position that will read from now.</returns>
	public static CosmosDbCdcPosition Now() => new(null, DateTimeOffset.UtcNow);

	/// <summary>
	/// Creates a position from a specific timestamp.
	/// </summary>
	/// <param name="timestamp">The timestamp to start from.</param>
	/// <returns>A position that will read from the specified time.</returns>
	public static CosmosDbCdcPosition FromTimestamp(DateTimeOffset timestamp) => new(null, timestamp);

	/// <summary>
	/// Creates a position from a continuation token.
	/// </summary>
	/// <param name="continuationToken">The continuation token.</param>
	/// <returns>A position that will resume from the token.</returns>
	public static CosmosDbCdcPosition FromContinuationToken(string continuationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(continuationToken);
		return new CosmosDbCdcPosition(continuationToken, null);
	}

	/// <summary>
	/// Deserializes a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <returns>The deserialized position.</returns>
	/// <exception cref="FormatException">Thrown if the string format is invalid.</exception>
	public static CosmosDbCdcPosition FromBase64(string base64)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(base64);

		try
		{
			var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

			if (decoded.StartsWith("T:", StringComparison.Ordinal))
			{
				return FromContinuationToken(decoded[2..]);
			}

			if (decoded.StartsWith("D:", StringComparison.Ordinal))
			{
				var timestamp = DateTimeOffset.Parse(decoded[2..], System.Globalization.CultureInfo.InvariantCulture);
				return FromTimestamp(timestamp);
			}

			if (decoded.StartsWith("B:", StringComparison.Ordinal))
			{
				return Beginning();
			}

			throw new FormatException(Resources.ErrorMessages.InvalidPositionFormat);
		}
		catch (FormatException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new FormatException(Resources.ErrorMessages.FailedToParsePositionFromBase64, ex);
		}
	}

	/// <summary>
	/// Attempts to deserialize a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <param name="position">The deserialized position if successful.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryFromBase64(string? base64, out CosmosDbCdcPosition position)
	{
		if (string.IsNullOrWhiteSpace(base64))
		{
			position = Beginning();
			return false;
		}

		try
		{
			position = FromBase64(base64);
			return true;
		}
		catch
		{
			position = Beginning();
			return false;
		}
	}


	/// <inheritdoc/>
	public override string ToToken() => ToBase64();

	/// <inheritdoc/>
	public override bool Equals(ChangePosition? other) => other is CosmosDbCdcPosition cosmos && Equals(cosmos);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is CosmosDbCdcPosition cosmos && Equals(cosmos);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(ContinuationToken, Timestamp);

	/// <summary>
	/// Serializes this position to a base64 string for storage.
	/// </summary>
	/// <returns>A base64-encoded string representation.</returns>
	public string ToBase64()
	{
		if (ContinuationToken is not null)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes($"T:{ContinuationToken}"));
		}

		if (Timestamp.HasValue)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes($"D:{Timestamp.Value:O}"));
		}

		return Convert.ToBase64String(Encoding.UTF8.GetBytes("B:"));
	}

	/// <inheritdoc/>
	public bool Equals(CosmosDbCdcPosition? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return string.Equals(ContinuationToken, other.ContinuationToken, StringComparison.Ordinal)
			   && Timestamp == other.Timestamp;
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		if (ContinuationToken is not null)
		{
			var preview = ContinuationToken.Length > 50
				? ContinuationToken[..50] + "..."
				: ContinuationToken;
			return $"Token({preview})";
		}

		if (Timestamp.HasValue)
		{
			return $"Timestamp({Timestamp.Value:O})";
		}

		return "Beginning";
	}
}
