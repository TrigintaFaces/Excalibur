// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using MongoDB.Bson;

namespace Excalibur.Cdc.MongoDB;

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

	// Field names of the envelope written by TokenString when, and only when, the mode is StartAfter.
	// A bare resume token is a document of shape { "_data": "..." }, so a document carrying exactly these
	// two fields is unambiguously ours and an older stored token still parses as an ordinary ResumeAfter
	// checkpoint.
	private const string ResumeModeField = "excaliburResumeMode";
	private const string TokenField = "excaliburToken";
	private const string StartAfterModeValue = "startAfter";

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcPosition"/> struct for an ordinary
	/// checkpoint, reopened with <c>resumeAfter</c>.
	/// </summary>
	/// <param name="resumeToken">The resume token from a change stream event.</param>
	public MongoDbCdcPosition(BsonDocument? resumeToken)
		: this(resumeToken, MongoDbChangeStreamResumeMode.ResumeAfter)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcPosition"/> struct.
	/// </summary>
	/// <param name="resumeToken">The resume token from a change stream event.</param>
	/// <param name="resumeMode">
	/// Which change-stream option this token must be reopened with. A token taken from an
	/// <c>invalidate</c> event is only usable as <see cref="MongoDbChangeStreamResumeMode.StartAfter"/>.
	/// </param>
	public MongoDbCdcPosition(BsonDocument? resumeToken, MongoDbChangeStreamResumeMode resumeMode)
	{
		ResumeToken = resumeToken;
		ResumeMode = resumeMode;
	}

	/// <summary>
	/// Gets the resume token as a BSON document.
	/// </summary>
	public BsonDocument? ResumeToken { get; }

	/// <summary>
	/// Gets the change-stream option this token must be reopened with.
	/// </summary>
	public MongoDbChangeStreamResumeMode ResumeMode { get; }

	/// <summary>
	/// Gets a value indicating whether this position is valid (has a resume token).
	/// </summary>
	public bool IsValid => ResumeToken is not null;

	/// <summary>
	/// Gets the resume token as a JSON string for storage/serialization.
	/// </summary>
	/// <remarks>
	/// A <see cref="MongoDbChangeStreamResumeMode.StartAfter"/> position serializes as an envelope
	/// carrying both the token and the mode, because a restarted process that reopened such a token with
	/// <c>resumeAfter</c> would land back before the invalidation it had already passed.
	/// </remarks>
	public string? TokenString =>
		ResumeToken is null
			? null
			: ResumeMode == MongoDbChangeStreamResumeMode.StartAfter
				? new BsonDocument
				{
					{ ResumeModeField, StartAfterModeValue },
					{ TokenField, ResumeToken },
				}.ToJson()
				: ResumeToken.ToJson();

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
			return FromDocument(BsonDocument.Parse(tokenString));
		}
		catch
		{
			return Start;
		}
	}

	/// <summary>
	/// Rebuilds a position from a parsed storage document, unwrapping the resume-mode envelope when one
	/// is present and treating anything else as a bare <c>resumeAfter</c> token.
	/// </summary>
	private static MongoDbCdcPosition FromDocument(BsonDocument document)
	{
		if (document.ElementCount == 2 &&
			document.TryGetValue(ResumeModeField, out var mode) &&
			mode.IsString &&
			string.Equals(mode.AsString, StartAfterModeValue, StringComparison.Ordinal) &&
			document.TryGetValue(TokenField, out var token) &&
			token is BsonDocument tokenDocument)
		{
			return new MongoDbCdcPosition(tokenDocument, MongoDbChangeStreamResumeMode.StartAfter);
		}

		return new MongoDbCdcPosition(document);
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
			result = FromDocument(BsonDocument.Parse(tokenString));
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
		// The mode is part of the identity: the same token reopened with startAfter lands past an
		// invalidation and with resumeAfter lands before it, so two positions that differ only in mode
		// name different resume points.
		if (ResumeMode != other.ResumeMode)
		{
			return false;
		}

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
		return HashCode.Combine(ResumeToken?.GetHashCode() ?? 0, (int)ResumeMode);
	}

	/// <summary>
	/// Converts this position to a <see cref="ChangePosition"/> for use with the unified
	/// <see cref="ICdcStateStore"/> contract.
	/// </summary>
	/// <returns>A <see cref="TokenChangePosition"/> representing this position.</returns>
	public ChangePosition ToChangePosition() =>
		IsValid ? new TokenChangePosition(TokenString!) : TokenChangePosition.Empty;

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
