// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Provides metrics for a channel message pump.
/// </summary>
public sealed class ChannelMessagePumpMetrics
{
	/// <summary>
	/// Gets or sets the total number of messages produced.
	/// </summary>
	/// <value>The current <see cref="MessagesProduced"/> value.</value>
	public long MessagesProduced { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages consumed.
	/// </summary>
	/// <value>The current <see cref="MessagesConsumed"/> value.</value>
	public long MessagesConsumed { get; set; }

	/// <summary>
	/// Gets or sets the current number of messages in the channel.
	/// </summary>
	/// <value>The current <see cref="CurrentQueueDepth"/> value.</value>
	public int CurrentQueueDepth { get; set; }

	/// <summary>
	/// Gets or sets the maximum queue depth reached.
	/// </summary>
	/// <value>The current <see cref="MaxQueueDepth"/> value.</value>
	public int MaxQueueDepth { get; set; }

	/// <summary>
	/// Gets or sets the number of messages that failed processing.
	/// </summary>
	/// <value>The current <see cref="MessagesFailed"/> value.</value>
	public long MessagesFailed { get; set; }

	/// <summary>
	/// Gets or sets the number of messages that were acknowledged.
	/// </summary>
	/// <value>The current <see cref="MessagesAcknowledged"/> value.</value>
	public long MessagesAcknowledged { get; set; }

	/// <summary>
	/// Gets or sets the number of messages that were rejected.
	/// </summary>
	/// <value>The current <see cref="MessagesRejected"/> value.</value>
	public long MessagesRejected { get; set; }

	/// <summary>
	/// Gets or sets the average processing time in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageProcessingTimeMs"/> value.</value>
	public double AverageProcessingTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of when the pump started.
	/// </summary>
	/// <value>The current <see cref="StartedAt"/> value.</value>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last message produced.
	/// </summary>
	/// <value>The current <see cref="LastProducedAt"/> value.</value>
	public DateTimeOffset? LastProducedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last message consumed.
	/// </summary>
	/// <value>The current <see cref="LastConsumedAt"/> value.</value>
	public DateTimeOffset? LastConsumedAt { get; set; }
}
