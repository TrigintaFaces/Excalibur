// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Provides OTel metrics counters for event archival operations.
/// </summary>
internal static class ArchiveMetrics
{
	private static readonly Meter Meter = new("Excalibur.EventSourcing.Archive", "1.0.0");

	internal static readonly Counter<long> EventsArchived = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.archive.events_archived",
		description: "Total number of events moved to cold storage");

	internal static readonly Counter<long> EventsDeleted = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.archive.events_deleted",
		description: "Total number of events deleted from hot storage after archival");

	internal static readonly Counter<long> ColdReads = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.archive.cold_reads",
		description: "Number of read-through operations from cold storage");

	internal static readonly Counter<long> ArchiveErrors = Meter.CreateCounter<long>(
		"excalibur.eventsourcing.archive.errors",
		description: "Number of archive operation failures");

	internal static readonly Histogram<double> ArchiveDuration = Meter.CreateHistogram<double>(
		"excalibur.eventsourcing.archive.duration_seconds",
		description: "Duration of archive batch operations in seconds");
}
