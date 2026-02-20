// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Abstract base class representing a position in a Change Data Capture (CDC) stream.
/// </summary>
/// <remarks>
/// <para>
/// Each CDC provider has its own native position representation:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Provider</term>
/// <description>Position Type</description>
/// </listheader>
/// <item>
/// <term>SQL Server</term>
/// <description>Log Sequence Number (LSN) - byte array</description>
/// </item>
/// <item>
/// <term>Postgres</term>
/// <description>WAL Log Sequence Number - 64-bit value (e.g., "0/1234ABCD")</description>
/// </item>
/// <item>
/// <term>MongoDB</term>
/// <description>Resume Token - BsonDocument</description>
/// </item>
/// <item>
/// <term>CosmosDB</term>
/// <description>Continuation Token - string</description>
/// </item>
/// <item>
/// <term>DynamoDB</term>
/// <description>Shard Iterator - string</description>
/// </item>
/// <item>
/// <term>Firestore</term>
/// <description>Snapshot time or document change sequence</description>
/// </item>
/// </list>
/// <para>
/// Provider implementations derive from this class to provide strongly-typed positions
/// while maintaining a common interface for position serialization and comparison.
/// </para>
/// <para>
/// <b>Implementation Guidelines:</b>
/// <list type="bullet">
/// <item><description>Override <see cref="ToToken"/> to serialize position for storage/resumption.</description></item>
/// <item><description>Override <see cref="IsValid"/> to indicate whether the position represents a valid point in the stream.</description></item>
/// <item><description>Provide a static <c>Parse(string token)</c> or <c>TryParse</c> method for deserialization.</description></item>
/// <item><description>Implement <see cref="IComparable{T}"/> if position ordering is meaningful for the provider.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Provider-specific position example (Postgres)
/// public sealed class PostgresCdcPosition : ChangePosition
/// {
///     public NpgsqlLogSequenceNumber Lsn { get; }
///
///     public PostgresCdcPosition(NpgsqlLogSequenceNumber lsn)
///     {
///         Lsn = lsn;
///     }
///
///     public override string ToToken() => Lsn.ToString();
///     public override bool IsValid => Lsn != NpgsqlLogSequenceNumber.Invalid;
///
///     public static PostgresCdcPosition Parse(string token) =>
///         new(NpgsqlLogSequenceNumber.Parse(token));
/// }
/// </code>
/// </example>
public abstract class ChangePosition : IEquatable<ChangePosition>
{
	/// <summary>
	/// Gets a value indicating whether this position represents a valid point in the CDC stream.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this position can be used to resume CDC processing;
	/// <see langword="false"/> if this is an invalid or uninitialized position.
	/// </value>
	/// <remarks>
	/// <para>
	/// Invalid positions typically represent:
	/// <list type="bullet">
	/// <item><description>Uninitialized default positions</description></item>
	/// <item><description>Positions that have been invalidated (e.g., WAL segment recycled)</description></item>
	/// <item><description>Sentinel values indicating "start from beginning" or "no checkpoint"</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public abstract bool IsValid { get; }

	/// <summary>
	/// Gets the timestamp associated with this position, if available.
	/// </summary>
	/// <value>
	/// The timestamp when the change at this position occurred, or <see langword="null"/>
	/// if the provider does not support position timestamps.
	/// </value>
	/// <remarks>
	/// <para>
	/// Not all providers include timestamps in their position representation.
	/// When available, this can be used for time-based CDC resumption or monitoring.
	/// </para>
	/// </remarks>
	public virtual DateTimeOffset? Timestamp => null;

	/// <summary>
	/// Determines whether two positions are equal.
	/// </summary>
	/// <param name="left">The first position to compare.</param>
	/// <param name="right">The second position to compare.</param>
	/// <returns><see langword="true"/> if the positions are equal; otherwise, <see langword="false"/>.</returns>
	public static bool operator ==(ChangePosition? left, ChangePosition? right) =>
		left is null ? right is null : left.Equals(right);

	/// <summary>
	/// Determines whether two positions are not equal.
	/// </summary>
	/// <param name="left">The first position to compare.</param>
	/// <param name="right">The second position to compare.</param>
	/// <returns><see langword="true"/> if the positions are not equal; otherwise, <see langword="false"/>.</returns>
	public static bool operator !=(ChangePosition? left, ChangePosition? right) => !(left == right);

	/// <summary>
	/// Converts this position to a string token suitable for storage and resumption.
	/// </summary>
	/// <returns>
	/// A string representation of this position that can be persisted and later
	/// used to resume CDC processing from this point.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The token format is provider-specific but must be round-trippable through
	/// the corresponding <c>Parse</c> method on the derived type.
	/// </para>
	/// </remarks>
	public abstract string ToToken();

	/// <inheritdoc/>
	public abstract bool Equals(ChangePosition? other);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is ChangePosition other && Equals(other);

	/// <inheritdoc/>
	public abstract override int GetHashCode();

	/// <inheritdoc/>
	public override string ToString() => ToToken();
}

/// <summary>
/// Represents a simple token-based change position for providers that use string continuation tokens.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is suitable for providers like CosmosDB and DynamoDB that use
/// opaque string tokens for position tracking. For providers with structured positions
/// (e.g., Postgres LSN, SQL Server LSN/SeqVal), derive directly from <see cref="ChangePosition"/>.
/// </para>
/// </remarks>
public sealed class TokenChangePosition : ChangePosition
{
	/// <summary>
	/// Represents an invalid or uninitialized position.
	/// </summary>
	public static readonly TokenChangePosition Empty = new(string.Empty);

	private readonly DateTimeOffset? _timestamp;

	/// <summary>
	/// Initializes a new instance of the <see cref="TokenChangePosition"/> class.
	/// </summary>
	/// <param name="token">The continuation token representing this position.</param>
	/// <param name="timestamp">Optional timestamp associated with this position.</param>
	public TokenChangePosition(string token, DateTimeOffset? timestamp = null)
	{
		Token = token ?? throw new ArgumentNullException(nameof(token));
		_timestamp = timestamp;
	}

	/// <summary>
	/// Gets the continuation token representing this position.
	/// </summary>
	public string Token { get; }

	/// <inheritdoc/>
	public override bool IsValid => !string.IsNullOrEmpty(Token);

	/// <inheritdoc/>
	public override DateTimeOffset? Timestamp => _timestamp;

	/// <summary>
	/// Parses a token string to create a <see cref="TokenChangePosition"/>.
	/// </summary>
	/// <param name="token">The token to parse.</param>
	/// <returns>A new <see cref="TokenChangePosition"/>.</returns>
	public static TokenChangePosition Parse(string token) => new(token);

	/// <summary>
	/// Tries to parse a token string to create a <see cref="TokenChangePosition"/>.
	/// </summary>
	/// <param name="token">The token to parse.</param>
	/// <param name="position">When successful, contains the parsed position.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryParse(string? token, out TokenChangePosition position)
	{
		if (string.IsNullOrEmpty(token))
		{
			position = Empty;
			return false;
		}

		position = new TokenChangePosition(token);
		return true;
	}

	/// <inheritdoc/>
	public override string ToToken() => Token;

	/// <inheritdoc/>
	public override bool Equals(ChangePosition? other) =>
		other is TokenChangePosition token && string.Equals(Token, token.Token, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override int GetHashCode() => Token.GetHashCode(StringComparison.Ordinal);
}
