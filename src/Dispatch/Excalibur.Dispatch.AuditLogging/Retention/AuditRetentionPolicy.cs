// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Retention;

/// <summary>
/// Represents the current retention policy.
/// </summary>
public sealed record AuditRetentionPolicy
{
	/// <summary>
	/// Gets the retention period for audit events.
	/// </summary>
	public required TimeSpan RetentionPeriod { get; init; }

	/// <summary>
	/// Gets the cleanup interval.
	/// </summary>
	public required TimeSpan CleanupInterval { get; init; }

	/// <summary>
	/// Gets the batch size for cleanup operations.
	/// </summary>
	public required int BatchSize { get; init; }

	/// <summary>
	/// Gets a value indicating whether events are archived before deletion.
	/// </summary>
	public required bool ArchiveBeforeDelete { get; init; }
}
