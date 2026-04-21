// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Provides metrics counters for auto-snapshot operations.
/// </summary>
/// <remarks>
/// <para>
/// Metrics:
/// <list type="bullet">
/// <item><c>excalibur.eventsourcing.auto_snapshot.created</c> -- Counter of successfully created auto-snapshots</item>
/// <item><c>excalibur.eventsourcing.auto_snapshot.failed</c> -- Counter of failed auto-snapshot attempts</item>
/// <item><c>excalibur.eventsourcing.auto_snapshot.skipped</c> -- Counter of evaluations that did not trigger a snapshot</item>
/// </list>
/// </para>
/// </remarks>
internal static class AutoSnapshotMetrics
{
	private static readonly Meter Meter = new("Excalibur.EventSourcing.AutoSnapshot", "1.0.0");

	internal static readonly Counter<long> Created = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.auto_snapshot.created",
		description: "Number of auto-snapshots successfully created");

	internal static readonly Counter<long> Failed = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.auto_snapshot.failed",
		description: "Number of auto-snapshot creation failures");

	internal static readonly Counter<long> Evaluated = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.auto_snapshot.evaluated",
		description: "Number of auto-snapshot policy evaluations");
}
