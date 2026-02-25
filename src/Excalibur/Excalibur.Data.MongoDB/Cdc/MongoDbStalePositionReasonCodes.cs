// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Provides standardized reason codes for MongoDB CDC stale position scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These codes categorize why a change stream resume token became invalid in MongoDB.
/// Unlike Postgres logical replication slots, MongoDB stale positions typically occur due to:
/// <list type="bullet">
/// <item><description>Resume token no longer available in oplog</description></item>
/// <item><description>Collection dropped or renamed</description></item>
/// <item><description>Shard migration invalidating the stream</description></item>
/// <item><description>Change stream explicitly invalidated</description></item>
/// </list>
/// </para>
/// <para>
/// These codes are used in <c>CdcPositionResetEventArgs.ReasonCode</c> to enable consistent
/// logging, alerting, and handling across different stale position scenarios.
/// </para>
/// </remarks>
public static class MongoDbStalePositionReasonCodes
{
	/// <summary>
	/// The resume token is no longer available in the oplog.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the oplog has rolled over and the position corresponding to the
	/// resume token has been overwritten. MongoDB error code 136 (ChangeStreamHistoryLost).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Oplog size too small for change stream consumption rate</description></item>
	/// <item><description>Consumer offline for too long</description></item>
	/// <item><description>High write volume causing rapid oplog rotation</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ResumeTokenNotFound = "MONGODB_RESUME_TOKEN_NOT_FOUND";

	/// <summary>
	/// The resume token format or content is invalid.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the resume token cannot be parsed or is corrupted.
	/// MongoDB error code 286 (ChangeStreamFatalError).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Token serialization/deserialization error</description></item>
	/// <item><description>Token from incompatible MongoDB version</description></item>
	/// <item><description>Manual token modification or corruption</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string InvalidResumeToken = "MONGODB_INVALID_RESUME_TOKEN";

	/// <summary>
	/// The collection being watched was dropped.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the source collection no longer exists.
	/// MongoDB error code 26 (NamespaceNotFound).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Collection explicitly dropped</description></item>
	/// <item><description>Database dropped</description></item>
	/// <item><description>Collection recreated (different UUID)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string CollectionDropped = "MONGODB_COLLECTION_DROPPED";

	/// <summary>
	/// The collection being watched was renamed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the source collection has been renamed.
	/// MongoDB error code 73 (InvalidNamespace).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Collection renamed via renameCollection command</description></item>
	/// <item><description>Namespace changed during migration</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string CollectionRenamed = "MONGODB_COLLECTION_RENAMED";

	/// <summary>
	/// The change stream was invalidated due to shard migration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs in sharded clusters when the chunk containing the resume position
	/// has been migrated. MongoDB error code 133 (StaleShardVersion).
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Chunk migration during balancing</description></item>
	/// <item><description>Shard removed from cluster</description></item>
	/// <item><description>Collection resharded</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ShardMigration = "MONGODB_SHARD_MIGRATION";

	/// <summary>
	/// The change stream received an explicit invalidate event.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when MongoDB sends an invalidate event, signaling that the
	/// change stream is no longer valid and must be restarted.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Collection dropped while stream active</description></item>
	/// <item><description>Database dropped while stream active</description></item>
	/// <item><description>dropDatabase command executed</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string StreamInvalidated = "MONGODB_STREAM_INVALIDATED";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the MongoDB error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "MONGODB_UNKNOWN";

	/// <summary>
	/// Determines the reason code from a MongoDB error code.
	/// </summary>
	/// <param name="errorCode">The MongoDB error code.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// <para>
	/// MongoDB error codes relevant to change streams:
	/// <list type="bullet">
	/// <item><description>136 - ChangeStreamHistoryLost (resume token not in oplog)</description></item>
	/// <item><description>286 - ChangeStreamFatalError (invalid resume token)</description></item>
	/// <item><description>26 - NamespaceNotFound (collection dropped)</description></item>
	/// <item><description>73 - InvalidNamespace (collection renamed)</description></item>
	/// <item><description>133 - StaleShardVersion (shard migration)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static string FromErrorCode(int errorCode) =>
		errorCode switch
		{
			// ChangeStreamHistoryLost - resume token not in oplog
			136 => ResumeTokenNotFound,
			// ChangeStreamFatalError - invalid resume token
			286 => InvalidResumeToken,
			// NamespaceNotFound - collection dropped
			26 => CollectionDropped,
			// InvalidNamespace - collection renamed
			73 => CollectionRenamed,
			// StaleShardVersion - shard migration
			133 => ShardMigration,
			_ => Unknown
		};

	/// <summary>
	/// Determines the reason code from a MongoDB error message pattern.
	/// </summary>
	/// <param name="errorMessage">The MongoDB error message.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// This method provides fallback detection when error codes are not available
	/// by analyzing common error message patterns.
	/// </remarks>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("RESUME TOKEN", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("RESUMETOKEN", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("RESUME AFTER", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("OPLOG", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("CHANGE STREAM HISTORY", StringComparison.OrdinalIgnoreCase))
		{
			return ResumeTokenNotFound;
		}

		if (errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("RESUME", StringComparison.OrdinalIgnoreCase)))
		{
			return InvalidResumeToken;
		}

		if (errorMessage.Contains("COLLECTION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("DROPPED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase)))
		{
			return CollectionDropped;
		}

		if (errorMessage.Contains("RENAMED", StringComparison.OrdinalIgnoreCase) ||
			(errorMessage.Contains("NAMESPACE", StringComparison.OrdinalIgnoreCase) &&
			errorMessage.Contains("CHANGED", StringComparison.OrdinalIgnoreCase)))
		{
			return CollectionRenamed;
		}

		if (errorMessage.Contains("SHARD", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("MIGRATION", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("STALE", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("VERSION", StringComparison.OrdinalIgnoreCase)))
		{
			return ShardMigration;
		}

		if (errorMessage.Contains("INVALIDATE", StringComparison.OrdinalIgnoreCase) ||
			(errorMessage.Contains("CHANGE STREAM", StringComparison.OrdinalIgnoreCase) &&
			errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase)))
		{
			return StreamInvalidated;
		}

		return Unknown;
	}
}
