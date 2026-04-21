// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Configuration options for automatic snapshot creation after <c>SaveAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// When any threshold is met, a snapshot is created inline after events are committed.
/// Snapshot failure is best-effort -- it does not fail the save operation.
/// </para>
/// <para>
/// All thresholds are disabled by default (null). When multiple thresholds are set,
/// any single match triggers a snapshot.
/// </para>
/// <para>
/// Per-aggregate-type overrides are supported via named options:
/// <c>UseAutoSnapshots&lt;TAggregate&gt;(options =&gt; { ... })</c>.
/// </para>
/// </remarks>
public sealed class AutoSnapshotOptions
{
	/// <summary>
	/// Gets or sets the number of events since the last snapshot that triggers a new snapshot.
	/// </summary>
	/// <value>The event count threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public int? EventCountThreshold { get; set; }

	/// <summary>
	/// Gets or sets the time elapsed since the last snapshot that triggers a new snapshot.
	/// </summary>
	/// <value>The time threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public TimeSpan? TimeThreshold { get; set; }

	/// <summary>
	/// Gets or sets the aggregate version that triggers a new snapshot.
	/// </summary>
	/// <value>The version threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public long? VersionThreshold { get; set; }

	/// <summary>
	/// Gets or sets a custom policy function for advanced snapshot decision logic.
	/// </summary>
	/// <value>A function returning <see langword="true"/> to trigger a snapshot, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public Func<SnapshotDecisionContext, bool>? CustomPolicy { get; set; }
}
