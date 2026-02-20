// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents statistics for the dead letter queue.
/// </summary>
public sealed record DeadLetterQueueStatistics
{
	/// <summary>
	/// Gets the total number of messages in the dead letter queue.
	/// </summary>
	/// <value>
	/// The total number of messages in the dead letter queue.
	/// </value>
	public long TotalMessages { get; init; }

	/// <summary>
	/// Gets the number of messages dead lettered in the last hour.
	/// </summary>
	/// <value>
	/// The number of messages dead lettered in the last hour.
	/// </value>
	public long MessagesLastHour { get; init; }

	/// <summary>
	/// Gets the number of messages dead lettered in the last day.
	/// </summary>
	/// <value>
	/// The number of messages dead lettered in the last day.
	/// </value>
	public long MessagesLastDay { get; init; }

	/// <summary>
	/// Gets the most common dead letter reasons.
	/// </summary>
	/// <value>
	/// The most common dead letter reasons.
	/// </value>
	public IReadOnlyDictionary<string, long> ReasonCounts { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the timestamp when the statistics were collected.
	/// </summary>
	/// <value>
	/// The timestamp when the statistics were collected.
	/// </value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets a value indicating whether the dead letter queue is healthy (not growing too fast).
	/// </summary>
	/// <value>
	/// A value indicating whether the dead letter queue is healthy (not growing too fast).
	/// </value>
	public bool IsHealthy { get; init; }
}
