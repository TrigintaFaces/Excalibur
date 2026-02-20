// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Statistics about the dead letter queue.
/// </summary>
public sealed class DeadLetterStatistics
{
	/// <summary>
	/// Gets or sets the total number of messages in the DLQ.
	/// </summary>
	/// <value>The current <see cref="MessageCount"/> value.</value>
	public int MessageCount { get; set; }

	/// <summary>
	/// Gets or sets the average number of delivery attempts.
	/// </summary>
	/// <value>The current <see cref="AverageDeliveryAttempts"/> value.</value>
	public double AverageDeliveryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the age of the oldest message.
	/// </summary>
	/// <value>The current <see cref="OldestMessageAge"/> value.</value>
	public TimeSpan OldestMessageAge { get; set; }

	/// <summary>
	/// Gets or sets the age of the newest message.
	/// </summary>
	/// <value>The current <see cref="NewestMessageAge"/> value.</value>
	public TimeSpan NewestMessageAge { get; set; }

	/// <summary>
	/// Gets the breakdown by reason.
	/// </summary>
	/// <value>The current <see cref="ReasonBreakdown"/> value.</value>
	public Dictionary<string, int> ReasonBreakdown { get; } = [];

	/// <summary>
	/// Gets the breakdown by source queue.
	/// </summary>
	/// <value>The current <see cref="SourceBreakdown"/> value.</value>
	public Dictionary<string, int> SourceBreakdown { get; } = [];

	/// <summary>
	/// Gets the breakdown by message type.
	/// </summary>
	/// <value>The current <see cref="MessageTypeBreakdown"/> value.</value>
	public Dictionary<string, int> MessageTypeBreakdown { get; } = [];

	/// <summary>
	/// Gets or sets the size of the DLQ in bytes.
	/// </summary>
	/// <value>The current <see cref="SizeInBytes"/> value.</value>
	public long SizeInBytes { get; set; }

	/// <summary>
	/// Gets or sets when the statistics were generated.
	/// </summary>
	/// <value>The current <see cref="GeneratedAt"/> value.</value>
	public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
