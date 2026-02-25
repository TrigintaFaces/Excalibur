// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Provides standardized reason codes for Postgres CDC stale position scenarios.
/// </summary>
/// <remarks>
/// <para>
/// These codes categorize why a logical replication position became invalid in Postgres.
/// Unlike SQL Server CDC which uses LSN cleanup, Postgres stale positions typically occur due to:
/// <list type="bullet">
/// <item><description>WAL segment removal before consumption</description></item>
/// <item><description>Replication slot invalidation</description></item>
/// <item><description>Logical decoding plugin errors</description></item>
/// </list>
/// </para>
/// <para>
/// These codes are used in <c>CdcPositionResetEventArgs.ReasonCode</c> to enable consistent
/// logging, alerting, and handling across different stale position scenarios.
/// </para>
/// </remarks>
public static class PostgresStalePositionReasonCodes
{
	/// <summary>
	/// The WAL position is no longer available due to segment removal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the WAL segment containing the saved position has been removed
	/// by Postgres's WAL management. Typically corresponds to SQLSTATE 58P01.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>wal_keep_size or max_slot_wal_keep_size exceeded</description></item>
	/// <item><description>Replication slot marked as inactive and WAL recycled</description></item>
	/// <item><description>Manual WAL file removal</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string WalPositionStale = "WAL_POSITION_STALE";

	/// <summary>
	/// The replication slot is invalid or has been dropped.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the replication slot used for logical replication no longer exists
	/// or has been invalidated. Typically corresponds to SQLSTATE 55000.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Slot manually dropped via pg_drop_replication_slot()</description></item>
	/// <item><description>Slot invalidated due to wal_level change</description></item>
	/// <item><description>Slot exceeded max_slot_wal_keep_size and was invalidated</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string ReplicationSlotInvalid = "REPLICATION_SLOT_INVALID";

	/// <summary>
	/// A logical decoding error occurred while processing changes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This covers various logical decoding plugin errors (pgoutput) that prevent
	/// change processing from the saved position.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Output plugin mismatch</description></item>
	/// <item><description>Publication configuration changed</description></item>
	/// <item><description>Schema changes invalidating decoded data</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string LogicalDecodingError = "LOGICAL_DECODING_ERROR";

	/// <summary>
	/// The publication no longer exists or has been modified.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This occurs when the publication being subscribed to has been dropped or
	/// modified in a way that invalidates the current position.
	/// </para>
	/// <para>
	/// Common causes:
	/// <list type="bullet">
	/// <item><description>Publication dropped via DROP PUBLICATION</description></item>
	/// <item><description>Tables removed from publication</description></item>
	/// <item><description>Publication recreated with different settings</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public const string PublicationInvalid = "PUBLICATION_INVALID";

	/// <summary>
	/// The connection to the replication stream was lost unexpectedly.
	/// </summary>
	/// <remarks>
	/// This may indicate network issues, server restart, or replication timeout.
	/// The position may still be valid but requires reconnection.
	/// </remarks>
	public const string ReplicationStreamDisconnected = "REPLICATION_STREAM_DISCONNECTED";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the Postgres error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "UNKNOWN";

	/// <summary>
	/// Determines the reason code from a Postgres SQLSTATE code.
	/// </summary>
	/// <param name="sqlState">The Postgres SQLSTATE code (e.g., "58P01", "55000").</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// <para>
	/// Postgres SQLSTATE codes relevant to replication:
	/// <list type="bullet">
	/// <item><description>58P01 - could_not_access_file (WAL segment missing)</description></item>
	/// <item><description>55000 - object_not_in_prerequisite_state (slot invalid)</description></item>
	/// <item><description>42704 - undefined_object (publication/slot not found)</description></item>
	/// <item><description>08006 - connection_failure</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static string FromSqlState(string? sqlState) =>
		sqlState switch
		{
			// could_not_access_file - WAL segment removed
			"58P01" => WalPositionStale,
			// object_not_in_prerequisite_state - slot invalid/not active
			"55000" => ReplicationSlotInvalid,
			// undefined_object - publication or slot not found
			"42704" => PublicationInvalid,
			// connection_failure - replication connection lost
			"08006" => ReplicationStreamDisconnected,
			// protocol_violation - logical decoding issues
			"08P01" => LogicalDecodingError,
			// feature_not_supported - e.g., wal_level not logical
			"0A000" => LogicalDecodingError,
			_ => Unknown
		};

	/// <summary>
	/// Determines the reason code from a Postgres error message pattern.
	/// </summary>
	/// <param name="errorMessage">The Postgres error message.</param>
	/// <returns>The corresponding reason code.</returns>
	/// <remarks>
	/// This method provides fallback detection when SQLSTATE is not available
	/// by analyzing common error message patterns.
	/// </remarks>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("WAL SEGMENT", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("WAL FILE", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("REQUESTED WAL", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("WAL POSITION", StringComparison.OrdinalIgnoreCase))
		{
			return WalPositionStale;
		}

		if (errorMessage.Contains("REPLICATION SLOT", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DROPPED", StringComparison.OrdinalIgnoreCase)))
		{
			return ReplicationSlotInvalid;
		}

		if (errorMessage.Contains("PUBLICATION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DROPPED", StringComparison.OrdinalIgnoreCase)))
		{
			return PublicationInvalid;
		}

		if (errorMessage.Contains("LOGICAL DECODING", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("PGOUTPUT", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("OUTPUT PLUGIN", StringComparison.OrdinalIgnoreCase))
		{
			return LogicalDecodingError;
		}

		if (errorMessage.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("LOST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("CLOSED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("TERMINATED", StringComparison.OrdinalIgnoreCase)))
		{
			return ReplicationStreamDisconnected;
		}

		return Unknown;
	}
}
