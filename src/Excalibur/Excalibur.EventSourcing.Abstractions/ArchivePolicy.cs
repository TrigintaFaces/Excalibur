// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Configuration for event archival policies that determine which events
/// should be moved from hot to cold storage.
/// </summary>
/// <remarks>
/// <para>
/// Age and global-position thresholds are alternative eligibility triggers. Retention is an overriding
/// minimum hot suffix, ordered by aggregate version, and can be used without either trigger.
/// Only a prefix ending before the first ineligible payload can be archived; timestamps need not follow
/// version order. Already archived markers are not pending work. Erasure markers and unexplained missing
/// payloads stop the prefix. With no criteria configured, no events are eligible.
/// A covering snapshot is not required: payload removal requires a confirmed, recoverable contiguous
/// cold-history prefix. Snapshots are an optimization and cannot substitute for replayable history.
/// </para>
/// </remarks>
public sealed class ArchivePolicy
{
	/// <summary>
	/// Gets or sets the maximum age for events in hot storage.
	/// Events older than this are eligible for archival.
	/// </summary>
	/// <value>The max age threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public TimeSpan? MaxAge { get; set; }

	/// <summary>
	/// Gets or sets the maximum global position for events in hot storage.
	/// Events strictly before this global position are eligible for archival; equality is excluded.
	/// </summary>
	/// <value>The max position threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public long? MaxPosition { get; set; }

	/// <summary>
	/// Gets or sets the number of newest events, ordered by aggregate version, protected from archival.
	/// This overrides age and position eligibility. Used alone, it makes the older prefix eligible.
	/// </summary>
	/// <value>The retention count, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public int? RetainRecentCount { get; set; }
}
