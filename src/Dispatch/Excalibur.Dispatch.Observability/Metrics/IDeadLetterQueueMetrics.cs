// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Defines the contract for Dead Letter Queue metrics collection.
/// </summary>
public interface IDeadLetterQueueMetrics
{
	/// <summary>
	/// Gets the meter instance.
	/// </summary>
	Meter Meter { get; }

	/// <summary>
	/// Records a message being enqueued to the dead letter queue.
	/// </summary>
	/// <param name="messageType">The type of message that was dead lettered.</param>
	/// <param name="reason">The reason for dead lettering.</param>
	/// <param name="sourceQueue">The source queue name, or <see langword="null"/> if not applicable.</param>
	void RecordEnqueued(string messageType, string reason, string? sourceQueue);

	/// <summary>
	/// Records a message being replayed from the dead letter queue.
	/// </summary>
	/// <param name="messageType">The type of message that was replayed.</param>
	/// <param name="success">Whether the replay was successful.</param>
	void RecordReplayed(string messageType, bool success);

	/// <summary>
	/// Records messages being purged from the dead letter queue.
	/// </summary>
	/// <param name="count">The number of messages purged.</param>
	/// <param name="reason">The reason for purging (e.g., "age", "manual").</param>
	void RecordPurged(long count, string reason);

	/// <summary>
	/// Updates the current depth gauge for the dead letter queue.
	/// </summary>
	/// <param name="depth">The current number of messages in the queue.</param>
	/// <param name="queueName">The queue name, or <see langword="null"/> for the default queue.</param>
	void UpdateDepth(long depth, string? queueName);
}

/// <summary>
/// Extension methods for <see cref="IDeadLetterQueueMetrics"/> providing convenience overloads.
/// </summary>
public static class DeadLetterQueueMetricsExtensions
{
	/// <summary>
	/// Records a message being enqueued to the dead letter queue without specifying a source queue.
	/// </summary>
	/// <param name="metrics">The metrics instance.</param>
	/// <param name="messageType">The type of message that was dead lettered.</param>
	/// <param name="reason">The reason for dead lettering.</param>
	public static void RecordEnqueued(this IDeadLetterQueueMetrics metrics, string messageType, string reason)
		=> metrics.RecordEnqueued(messageType, reason, null);

	/// <summary>
	/// Updates the current depth gauge for the default dead letter queue.
	/// </summary>
	/// <param name="metrics">The metrics instance.</param>
	/// <param name="depth">The current number of messages in the queue.</param>
	public static void UpdateDepth(this IDeadLetterQueueMetrics metrics, long depth)
		=> metrics.UpdateDepth(depth, null);
}
