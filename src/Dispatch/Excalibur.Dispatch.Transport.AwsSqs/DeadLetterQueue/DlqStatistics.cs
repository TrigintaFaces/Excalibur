// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Statistics about the dead letter queue.
/// </summary>
public sealed class DlqStatistics
{
	/// <summary>
	/// Gets or sets the total number of messages in DLQ.
	/// </summary>
	/// <value>
	/// The total number of messages in DLQ.
	/// </value>
	public int TotalMessages { get; set; }

	/// <summary>
	/// Gets the number of messages by age bracket.
	/// </summary>
	/// <value>
	/// The number of messages by age bracket.
	/// </value>
	public Dictionary<string, int> MessagesByAge { get; } = [];

	/// <summary>
	/// Gets the number of messages by error type.
	/// </summary>
	/// <value>
	/// The number of messages by error type.
	/// </value>
	public Dictionary<string, int> MessagesByErrorType { get; } = [];

	/// <summary>
	/// Gets or sets the oldest message timestamp.
	/// </summary>
	/// <value>
	/// The oldest message timestamp.
	/// </value>
	public DateTime? OldestMessageTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the newest message timestamp.
	/// </summary>
	/// <value>
	/// The newest message timestamp.
	/// </value>
	public DateTime? NewestMessageTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the average retry count.
	/// </summary>
	/// <value>
	/// The average retry count.
	/// </value>
	public double AverageRetryCount { get; set; }

	/// <summary>
	/// Gets or sets the number of messages successfully redriven today.
	/// </summary>
	/// <value>
	/// The number of messages successfully redriven today.
	/// </value>
	public int RedrivenToday { get; set; }

	/// <summary>
	/// Gets or sets the number of messages archived today.
	/// </summary>
	/// <value>
	/// The number of messages archived today.
	/// </value>
	public int ArchivedToday { get; set; }

	/// <summary>
	/// Gets or sets the number of messages processed.
	/// </summary>
	/// <value>
	/// The number of messages processed.
	/// </value>
	public int MessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the number of messages requeued.
	/// </summary>
	/// <value>
	/// The number of messages requeued.
	/// </value>
	public int MessagesRequeued { get; set; }

	/// <summary>
	/// Gets or sets the number of messages discarded.
	/// </summary>
	/// <value>
	/// The number of messages discarded.
	/// </value>
	public int MessagesDiscarded { get; set; }

	/// <summary>
	/// Gets or sets when the statistics were generated.
	/// </summary>
	/// <value>
	/// When the statistics were generated.
	/// </value>
	public DateTimeOffset GeneratedAt { get; set; }
}
