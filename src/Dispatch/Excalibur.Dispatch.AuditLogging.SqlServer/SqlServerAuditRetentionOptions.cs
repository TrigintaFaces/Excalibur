// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging.SqlServer;

/// <summary>
/// Retention configuration options for the SQL Server audit store.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>DataProtectionOptions</c> pattern of separating retention policy
/// from core storage configuration.
/// </para>
/// </remarks>
public sealed class SqlServerAuditRetentionOptions
{
	/// <summary>
	/// Gets or sets the default retention period for audit events.
	/// Events older than this will be eligible for cleanup. Default is 7 years (SOC2 requirement).
	/// </summary>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7 * 365);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retention enforcement.
	/// </summary>
	public bool EnableRetentionEnforcement { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for retention cleanup operations. Default is 1 day.
	/// </summary>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets the maximum number of events to delete per cleanup batch. Default is 10000.
	/// </summary>
	public int CleanupBatchSize { get; set; } = 10000;
}
