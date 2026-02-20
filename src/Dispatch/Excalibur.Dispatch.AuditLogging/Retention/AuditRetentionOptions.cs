// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Retention;

/// <summary>
/// Configuration options for the audit retention service.
/// </summary>
public sealed class AuditRetentionOptions
{
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
