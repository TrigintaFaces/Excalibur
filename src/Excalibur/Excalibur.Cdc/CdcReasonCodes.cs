// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Provides standardized reason codes for CDC stale position scenarios across all providers.
/// </summary>
/// <remarks>
/// <para>
/// These codes are used in <see cref="CdcPositionResetEventArgs.ReasonCode"/> to categorize
/// why a CDC position became invalid. Providers may use these common codes or define
/// provider-specific codes with appropriate prefixes (e.g., <c>MONGODB_</c>, <c>COSMOSDB_</c>).
/// </para>
/// <para>
/// Common stale position scenarios include:
/// <list type="bullet">
/// <item><description>Position purged by cleanup/retention (SQL Server CDC, Postgres WAL)</description></item>
/// <item><description>Token expiration (MongoDB oplog, CosmosDB change feed)</description></item>
/// <item><description>Database restore or replication slot deletion</description></item>
/// <item><description>CDC/change stream configuration changes</description></item>
/// </list>
/// </para>
/// </remarks>
public static class CdcReasonCodes
{
	/// <summary>
	/// The position was purged by a cleanup or retention process.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the most common reason for stale positions. Examples:
	/// <list type="bullet">
	/// <item><description>SQL Server: CDC cleanup job (sys.sp_cdc_cleanup_change_tables)</description></item>
	/// <item><description>Postgres: WAL segments recycled past retention</description></item>
	/// <item><description>MongoDB: Oplog rolled over before consumption</description></item>
	/// <item><description>CosmosDB: Continuation token expired (7-day retention)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PositionPurged = "POSITION_PURGED";

	/// <summary>
	/// The position is invalid after a database backup and restore operation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when a database is restored from a backup taken before the saved position,
	/// or when a lower environment receives a copy of production data with different CDC history.
	/// </para>
	/// </remarks>
	public const string BackupRestore = "BACKUP_RESTORE";

	/// <summary>
	/// CDC or change tracking was disabled and re-enabled, invalidating existing positions.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When CDC is disabled and re-enabled, all capture instances are recreated and
	/// previous position values are no longer valid. Examples:
	/// <list type="bullet">
	/// <item><description>SQL Server: sys.sp_cdc_disable_db and sys.sp_cdc_enable_db</description></item>
	/// <item><description>Postgres: Replication slot dropped and recreated</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string CdcReenabled = "CDC_REENABLED";

	/// <summary>
	/// The position is outside the valid range of available change data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A general error when the position falls outside the min/max range available
	/// in the change tables. Provider-specific examples:
	/// <list type="bullet">
	/// <item><description>SQL Server: Error 22037 or 22029 (LSN out of range)</description></item>
	/// <item><description>Postgres: WAL position before consistent point</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PositionOutOfRange = "POSITION_OUT_OF_RANGE";

	/// <summary>
	/// The change stream or resume token has expired.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Document databases use tokens with finite lifetimes:
	/// <list type="bullet">
	/// <item><description>MongoDB: Resume token no longer in oplog (error 136)</description></item>
	/// <item><description>CosmosDB: Continuation token expired (HTTP 410)</description></item>
	/// <item><description>DynamoDB: Shard iterator expired (24-hour lifetime)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string TokenExpired = "TOKEN_EXPIRED";

	/// <summary>
	/// The capture instance, collection, or table no longer exists.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the source of change data has been dropped:
	/// <list type="bullet">
	/// <item><description>SQL Server: Capture instance dropped</description></item>
	/// <item><description>MongoDB: Collection dropped</description></item>
	/// <item><description>CosmosDB: Container deleted</description></item>
	/// <item><description>DynamoDB: Table deleted or stream disabled</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string SourceDropped = "SOURCE_DROPPED";

	/// <summary>
	/// The source was renamed, invalidating the position.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the collection, table, or namespace has been renamed:
	/// <list type="bullet">
	/// <item><description>MongoDB: Collection renamed</description></item>
	/// <item><description>Postgres: Table renamed</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string SourceRenamed = "SOURCE_RENAMED";

	/// <summary>
	/// A partition split or shard migration invalidated the position.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Distributed databases may reorganize partitions:
	/// <list type="bullet">
	/// <item><description>CosmosDB: Partition split due to storage growth</description></item>
	/// <item><description>MongoDB: Shard migration in sharded clusters</description></item>
	/// <item><description>DynamoDB: Shard split in DynamoDB Streams</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PartitionChanged = "PARTITION_CHANGED";

	/// <summary>
	/// The change stream was explicitly invalidated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The change stream sent an explicit invalidation event:
	/// <list type="bullet">
	/// <item><description>MongoDB: Invalidate event in change stream</description></item>
	/// <item><description>Firestore: Listen target removed</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string StreamInvalidated = "STREAM_INVALIDATED";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the provider error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "UNKNOWN";

	/// <summary>
	/// Determines if a reason code indicates a recoverable scenario.
	/// </summary>
	/// <param name="reasonCode">The reason code to check.</param>
	/// <returns>
	/// <see langword="true"/> if the scenario is typically recoverable by resetting
	/// to earliest or latest position; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Recoverable scenarios are those where the consumer can safely resume from
	/// a different position. Non-recoverable scenarios typically require manual intervention.
	/// </para>
	/// </remarks>
	public static bool IsRecoverable(string? reasonCode)
	{
		if (string.IsNullOrEmpty(reasonCode))
		{
			return false;
		}

		// Check known standard codes first (before the underscore check for provider-prefixed codes)
		switch (reasonCode)
		{
			case PositionPurged:
			case BackupRestore:
			case CdcReenabled:
			case PositionOutOfRange:
			case TokenExpired:
			case PartitionChanged:
			case StreamInvalidated:
				return true;
			case SourceDropped: // Requires intervention - source is gone
			case SourceRenamed: // Requires configuration update
			case Unknown: // Unknown reasons need investigation
				return false;
		}

		// Provider-prefixed codes (e.g., MONGODB_OPLOG_ROLLOVER) are generally recoverable
		if (reasonCode.Contains('_', StringComparison.Ordinal) && !reasonCode.StartsWith("UNKNOWN", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// Default to recoverable for unknown codes without underscores
		return true;
	}
}
