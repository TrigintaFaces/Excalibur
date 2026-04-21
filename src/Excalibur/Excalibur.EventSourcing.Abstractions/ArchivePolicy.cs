// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Configuration for event archival policies that determine which events
/// should be moved from hot to cold storage.
/// </summary>
/// <remarks>
/// <para>
/// Multiple criteria can be combined. Events matching any criterion are eligible
/// for archival. Events are only archived for aggregates that have a snapshot
/// covering the archived version range.
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
	/// Events before this position are eligible for archival.
	/// </summary>
	/// <value>The max position threshold, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public long? MaxPosition { get; set; }

	/// <summary>
	/// Gets or sets the number of most recent events to retain per aggregate in hot storage.
	/// </summary>
	/// <value>The retention count, or <see langword="null"/> to disable. Default is <see langword="null"/>.</value>
	public int? RetainRecentCount { get; set; }
}
