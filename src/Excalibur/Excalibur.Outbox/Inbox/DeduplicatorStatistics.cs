// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Outbox.Inbox;

/// <summary>
/// Statistics about the current state of the deduplicator.
/// </summary>
public sealed class DeduplicatorStatistics
{
	/// <summary>
	/// Gets the total number of entries in the deduplicator.
	/// </summary>
	/// <value>The current <see cref="TotalEntries"/> value.</value>
	public int TotalEntries { get; init; }

	/// <summary>
	/// Gets the number of expired entries awaiting cleanup.
	/// </summary>
	/// <value>The current <see cref="ExpiredEntries"/> value.</value>
	public int ExpiredEntries { get; init; }

	/// <summary>
	/// Gets the timestamp of the oldest entry.
	/// </summary>
	/// <value>The current <see cref="OldestEntry"/> value.</value>
	public DateTimeOffset OldestEntry { get; init; }

	/// <summary>
	/// Gets the timestamp of the newest entry.
	/// </summary>
	/// <value>The current <see cref="NewestEntry"/> value.</value>
	public DateTimeOffset NewestEntry { get; init; }

	/// <summary>
	/// Gets the average age of entries.
	/// </summary>
	/// <value>The current <see cref="AverageAge"/> value.</value>
	public TimeSpan AverageAge { get; init; }
}
