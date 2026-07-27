// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.Retention;

/// <summary>
/// Configuration options for the audit retention service.
/// </summary>
public sealed class AuditRetentionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether expired audit events are actually removed.
	/// </summary>
	/// <value>
	/// <see langword="true"/> — the default — to enforce the retention policy by deleting expired events;
	/// <see langword="false"/> to leave every event in place.
	/// </value>
	/// <remarks>
	/// <para>
	/// The default is <see langword="true"/> deliberately: a host that has gone to the trouble of
	/// registering retention has asked for a retention policy, and defaulting to "configured but not
	/// enforced" would be the false-control shape this option exists to avoid. Disabling is an explicit
	/// act.
	/// </para>
	/// <para>
	/// When disabled, enforcement does not run and says so — it does not report a completed pass. Nothing
	/// is deleted, and the absence of deletion is visible in the log rather than inferred from silence.
	/// </para>
	/// </remarks>
	public bool EnableRetentionEnforcement { get; set; } = true;

	/// <summary>
	/// Gets or sets the retention period for audit events.
	/// Events older than this are eligible for cleanup.
	/// </summary>
	/// <remarks>
	/// Default is 7 years (SOC2 requirement).
	/// </remarks>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7 * 365);

	/// <summary>
	/// Gets or sets the interval between cleanup runs.
	/// </summary>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets the maximum number of events to delete per cleanup batch.
	/// </summary>
	public int BatchSize { get; set; } = 10000;

	/// <summary>
	/// Gets or sets a value indicating whether to archive events before deleting them.
	/// </summary>
	public bool ArchiveBeforeDelete { get; set; }
}
