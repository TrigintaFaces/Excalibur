// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using MongoDB.Bson;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Represents a position in the MongoDB change stream using a resume token.
/// </summary>
/// <remarks>
/// The resume token is an opaque <see cref="BsonDocument"/> provided by MongoDB
/// that allows resuming a change stream from a specific point.
/// </remarks>
public readonly struct MongoDbCdcPosition : IEquatable<MongoDbCdcPosition>
{
	/// <summary>
	/// Represents the start position (no resume token).
	/// </summary>
	public static readonly MongoDbCdcPosition Start = new(null);

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcPosition"/> struct.
	/// </summary>
	/// <param name="resumeToken">The resume token from a change stream event.</param>
	public MongoDbCdcPosition(BsonDocument? resumeToken)
	{
		ResumeToken = resumeToken;
	}

	/// <summary>
	/// Gets the resume token as a BSON document.
	/// </summary>
	public BsonDocument? ResumeToken { get; }

	/// <summary>
	/// Gets a value indicating whether this position is valid (has a resume token).
	/// </summary>
	public bool IsValid => ResumeToken is not null;

	/// <summary>
	/// Gets the resume token as a JSON string for storage/serialization.
	/// </summary>
	public string? TokenString => ResumeToken?.ToJson();

	/// <summary>
	/// Creates a position from a JSON string representation of the resume token.
	/// </summary>
	/// <param name="tokenString">The JSON string representation.</param>
	/// <returns>A new <see cref="MongoDbCdcPosition"/>.</returns>
	public static MongoDbCdcPosition FromString(string? tokenString)
	{
		if (string.IsNullOrWhiteSpace(tokenString))
		{
			return Start;
		}

		try
		{
			var document = BsonDocument.Parse(tokenString);
			return new MongoDbCdcPosition(document);
		}
		catch
		{
			return Start;
		}
	}

	/// <summary>
	/// Tries to parse a JSON string into a <see cref="MongoDbCdcPosition"/>.
	/// </summary>
	/// <param name="tokenString">The JSON string to parse.</param>
	/// <param name="result">The parsed position if successful.</param>
	/// <returns>True if parsing succeeded; otherwise, false.</returns>
	public static bool TryParse(string? tokenString, out MongoDbCdcPosition result)
	{
		if (string.IsNullOrWhiteSpace(tokenString))
		{
			result = Start;
			return true;
		}

		try
		{
			var document = BsonDocument.Parse(tokenString);
			result = new MongoDbCdcPosition(document);
			return true;
		}
		catch
		{
			result = Start;
			return false;
		}
	}

	/// <summary>
	/// Determines whether two positions are equal.
	/// </summary>
	public static bool operator ==(MongoDbCdcPosition left, MongoDbCdcPosition right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// Determines whether two positions are not equal.
	/// </summary>
	public static bool operator !=(MongoDbCdcPosition left, MongoDbCdcPosition right)
	{
		return !left.Equals(right);
	}

	/// <inheritdoc/>
	public bool Equals(MongoDbCdcPosition other)
	{
		if (ResumeToken is null && other.ResumeToken is null)
		{
			return true;
		}

		if (ResumeToken is null || other.ResumeToken is null)
		{
			return false;
		}

		return ResumeToken.Equals(other.ResumeToken);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return obj is MongoDbCdcPosition other && Equals(other);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return ResumeToken?.GetHashCode() ?? 0;
	}

	/// <summary>
	/// Converts this position to a <see cref="ChangePosition"/> for use with the unified
	/// <see cref="ICdcStateStore"/> contract.
	/// </summary>
	/// <returns>A <see cref="TokenChangePosition"/> representing this position.</returns>
	public ChangePosition ToChangePosition() =>
		IsValid ? new TokenChangePosition(TokenString) : TokenChangePosition.Empty;

	/// <summary>
	/// Creates a <see cref="MongoDbCdcPosition"/> from a <see cref="ChangePosition"/>.
	/// </summary>
	/// <param name="position">The change position to convert.</param>
	/// <returns>A <see cref="MongoDbCdcPosition"/> parsed from the token.</returns>
	public static MongoDbCdcPosition FromChangePosition(ChangePosition? position) =>
		position is not null && position.IsValid ? FromString(position.ToToken()) : Start;

	/// <inheritdoc/>
	public override string ToString()
	{
		return TokenString ?? "<start>";
	}
}
