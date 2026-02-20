// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about ordering key message processing.
/// </summary>
public sealed class OrderingKeyStatistics
{
	/// <summary>
	/// Gets the total number of ordering keys.
	/// </summary>
	/// <value>
	/// The total number of ordering keys.
	/// </value>
	public int TotalOrderingKeys { get; init; }

	/// <summary>
	/// Gets the number of active ordering keys.
	/// </summary>
	/// <value>
	/// The number of active ordering keys.
	/// </value>
	public int ActiveOrderingKeys { get; init; }

	/// <summary>
	/// Gets the number of failed ordering keys.
	/// </summary>
	/// <value>
	/// The number of failed ordering keys.
	/// </value>
	public int FailedOrderingKeys { get; init; }

	/// <summary>
	/// Gets the total messages processed (used by OrderingKeyManager).
	/// </summary>
	/// <value>
	/// The total messages processed.
	/// </value>
	public long TotalMessagesProcessed { get; init; }

	/// <summary>
	/// Gets the total out-of-sequence messages.
	/// </summary>
	/// <value>
	/// The total out-of-sequence messages.
	/// </value>
	public long TotalOutOfSequenceMessages { get; init; }

	/// <summary>
	/// Gets the total number of messages processed (used by OrderingKeyProcessor).
	/// </summary>
	/// <value>
	/// The total number of messages processed.
	/// </value>
	public long TotalProcessed { get; init; }

	/// <summary>
	/// Gets the total number of errors encountered.
	/// </summary>
	/// <value>
	/// The total number of errors encountered.
	/// </value>
	public long TotalErrors { get; init; }

	/// <summary>
	/// Gets the average processing time in milliseconds.
	/// </summary>
	/// <value>
	/// The average processing time in milliseconds.
	/// </value>
	public double AverageProcessingTime { get; init; }

	/// <summary>
	/// Gets the average queue depth across all ordering keys.
	/// </summary>
	/// <value>
	/// The average queue depth across all ordering keys.
	/// </value>
	public double AverageQueueDepth { get; init; }

	/// <summary>
	/// Gets the per-queue statistics.
	/// </summary>
	/// <value>
	/// The per-queue statistics.
	/// </value>
	public IReadOnlyList<QueueStatistics> QueueStatistics { get; init; } = new List<QueueStatistics>();
}
