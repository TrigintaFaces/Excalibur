// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Statistics about poison messages.
/// </summary>
public sealed class PoisonMessageStatistics
{
	/// <summary>
	/// Gets or sets the total count of messages in the dead letter queue.
	/// </summary>
	/// <value>The current <see cref="TotalCount"/> value.</value>
	public long TotalCount { get; set; }

	/// <summary>
	/// Gets or sets the count of recent messages within the time window.
	/// </summary>
	/// <value>The current <see cref="RecentCount"/> value.</value>
	public int RecentCount { get; set; }

	/// <summary>
	/// Gets or sets the time window for recent message calculation.
	/// </summary>
	/// <value>The current <see cref="TimeWindow"/> value.</value>
	public TimeSpan TimeWindow { get; set; }

	/// <summary>
	/// Gets or sets the breakdown of messages by type.
	/// </summary>
	/// <value>The current <see cref="MessagesByType"/> value.</value>
	public Dictionary<string, int> MessagesByType { get; set; } = [];

	/// <summary>
	/// Gets or sets the breakdown of messages by reason.
	/// </summary>
	/// <value>The current <see cref="MessagesByReason"/> value.</value>
	public Dictionary<string, int> MessagesByReason { get; set; } = [];

	/// <summary>
	/// Gets or sets the date of the oldest message in the time window.
	/// </summary>
	/// <value>The current <see cref="OldestMessageDate"/> value.</value>
	public DateTimeOffset? OldestMessageDate { get; set; }

	/// <summary>
	/// Gets or sets the date of the newest message in the time window.
	/// </summary>
	/// <value>The current <see cref="NewestMessageDate"/> value.</value>
	public DateTimeOffset? NewestMessageDate { get; set; }
}
