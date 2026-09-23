// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.AuditLogging.SqlServer;

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
	/// The shipped default for <see cref="RetentionPeriod"/>, named so the registration that projects this
	/// block onto the core retention options can tell "the consumer left it alone" from "the consumer chose
	/// this value". Projecting unconditionally would let a provider default silently overwrite a window a
	/// host had already set on the core options, which is the direction that over-retains.
	/// </summary>
	internal static readonly TimeSpan DefaultRetentionPeriod = TimeSpan.FromDays(7 * 365);

	/// <summary>
	/// The shipped default for <see cref="CleanupInterval"/>. See <see cref="DefaultRetentionPeriod"/>.
	/// </summary>
	internal static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromDays(1);

	/// <summary>
	/// The shipped default for <see cref="EnableRetentionEnforcement"/>. See <see cref="DefaultRetentionPeriod"/>.
	/// </summary>
	internal const bool DefaultEnableRetentionEnforcement = true;

	/// <summary>
	/// Gets or sets the default retention period for audit events.
	/// Events older than this will be eligible for cleanup. Default is 7 years (SOC2 requirement).
	/// </summary>
	/// <remarks>
	/// Projected onto <c>AuditRetentionOptions.RetentionPeriod</c> at registration, which is the value the
	/// scheduled sweep deletes behind.
	/// </remarks>
	public TimeSpan RetentionPeriod { get; set; } = DefaultRetentionPeriod;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retention enforcement.
	/// </summary>
	/// <remarks>
	/// Projected onto <c>AuditRetentionOptions.EnableRetentionEnforcement</c> at registration.
	/// </remarks>
	public bool EnableRetentionEnforcement { get; set; } = DefaultEnableRetentionEnforcement;

	/// <summary>
	/// Gets or sets the interval for retention cleanup operations. Default is 1 day.
	/// </summary>
	/// <remarks>
	/// Projected onto <c>AuditRetentionOptions.CleanupInterval</c> at registration, which is the interval
	/// the retention background service waits between sweeps.
	/// </remarks>
	public TimeSpan CleanupInterval { get; set; } = DefaultCleanupInterval;

	/// <summary>
	/// Gets or sets the maximum number of events to delete per cleanup batch. Default is 10000.
	/// </summary>
	/// <remarks>
	/// NOT projected onto the core retention options: there is no store-agnostic batch size to project it
	/// onto, since each store's delete statement is dialect-specific. This is the SQL Server store's own
	/// delete batch size, read directly by the store.
	/// </remarks>
	public int CleanupBatchSize { get; set; } = 10000;
}
