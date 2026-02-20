// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides standardized reason codes for CDC stale position scenarios.
/// </summary>
/// <remarks>
/// These codes are used in <see cref="Excalibur.Cdc.CdcPositionResetEventArgs.ReasonCode"/> to categorize
/// why a CDC position became invalid. This enables consistent logging, alerting, and handling
/// across different stale position scenarios.
/// </remarks>
public static class StalePositionReasonCodes
{
	/// <summary>
	/// The position was purged by the CDC cleanup job (sys.sp_cdc_cleanup_change_tables).
	/// </summary>
	/// <remarks>
	/// This occurs when the CDC retention period expires and cleanup purges records
	/// older than the retention threshold. The saved LSN points to purged data.
	/// </remarks>
	public const string CdcCleanup = "CDC_CLEANUP";

	/// <summary>
	/// The position is invalid after a database backup and restore operation.
	/// </summary>
	/// <remarks>
	/// This occurs when a database is restored from a backup taken before the saved LSN,
	/// or when a lower environment receives a copy of production data with different CDC history.
	/// </remarks>
	public const string BackupRestore = "BACKUP_RESTORE";

	/// <summary>
	/// CDC was disabled and re-enabled on the database, invalidating existing positions.
	/// </summary>
	/// <remarks>
	/// When CDC is disabled (sys.sp_cdc_disable_db) and re-enabled, all capture instances
	/// are recreated and previous LSN values are no longer valid.
	/// </remarks>
	public const string CdcReenabled = "CDC_REENABLED";

	/// <summary>
	/// The LSN is outside the valid range of available change data.
	/// </summary>
	/// <remarks>
	/// This is a general error when the LSN falls outside the min/max LSN range
	/// available in the CDC change tables, typically SQL Error 22037 or 22029.
	/// </remarks>
	public const string LsnOutOfRange = "LSN_OUT_OF_RANGE";

	/// <summary>
	/// The capture instance no longer exists in the database.
	/// </summary>
	/// <remarks>
	/// This occurs when a table's CDC capture instance has been dropped,
	/// either explicitly or by dropping the source table.
	/// </remarks>
	public const string CaptureInstanceDropped = "CAPTURE_INSTANCE_DROPPED";

	/// <summary>
	/// The reason for the stale position could not be determined.
	/// </summary>
	/// <remarks>
	/// This is used when the specific cause cannot be identified from the SQL error
	/// or when an unexpected error occurs during position validation.
	/// </remarks>
	public const string Unknown = "UNKNOWN";

	/// <summary>
	/// Determines the reason code from a SQL Server error number.
	/// </summary>
	/// <param name="sqlErrorNumber">The SQL Server error number.</param>
	/// <returns>The corresponding reason code.</returns>
	public static string FromSqlError(int sqlErrorNumber) =>
		sqlErrorNumber switch
		{
			// Invalid from LSN specified for change data capture function
			22037 => LsnOutOfRange,
			// fn_cdc_get_all_changes_... was called with a from_lsn that is outside of valid range
			22029 => LsnOutOfRange,
			// CDC is not enabled for database
			22911 => CdcReenabled,
			// Capture instance not found
			22985 => CaptureInstanceDropped,
			_ => Unknown
		};
}
