// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Dead letter queue analytics data.
/// </summary>
public sealed class DeadLetterAnalytics
{
	/// <summary>
	/// Gets or sets the total number of messages in the dead letter queue.
	/// </summary>
	/// <value>
	/// The total number of messages in the dead letter queue.
	/// </value>
	public long TotalMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of messages by error type.
	/// </summary>
	/// <value>
	/// The number of messages by error type.
	/// </value>
	public Dictionary<string, long> MessagesByErrorType { get; set; } = [];

	/// <summary>
	/// Gets or sets the oldest message timestamp.
	/// </summary>
	/// <value>
	/// The oldest message timestamp.
	/// </value>
	public DateTimeOffset? OldestMessageTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the newest message timestamp.
	/// </summary>
	/// <value>
	/// The newest message timestamp.
	/// </value>
	public DateTimeOffset? NewestMessageTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the total count of dead letters.
	/// </summary>
	/// <value>
	/// The total count of dead letters.
	/// </value>
	public long TotalDeadLetters { get; set; }

	/// <summary>
	/// Gets or sets the last updated timestamp.
	/// </summary>
	/// <value>
	/// The last updated timestamp.
	/// </value>
	public DateTimeOffset LastUpdated { get; set; }

	/// <summary>
	/// Gets or sets when the message was last seen.
	/// </summary>
	/// <value>
	/// When the message was last seen.
	/// </value>
	public DateTimeOffset LastSeen { get; set; }

	/// <summary>
	/// Gets or sets the error reasons.
	/// </summary>
	/// <value>
	/// The error reasons.
	/// </value>
	public Dictionary<string, int> ErrorReasons { get; set; } = [];

	/// <summary>
	/// Gets or sets the original topics.
	/// </summary>
	/// <value>
	/// The original topics.
	/// </value>
	public Dictionary<string, int> OriginalTopics { get; set; } = [];

	/// <summary>
	/// Gets or sets the hourly distribution.
	/// </summary>
	/// <value>
	/// The hourly distribution.
	/// </value>
	public Dictionary<int, int> HourlyDistribution { get; set; } = [];

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	/// <value>
	/// The message type.
	/// </value>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the message type was first seen.
	/// </summary>
	/// <value>
	/// When the message type was first seen.
	/// </value>
	public DateTimeOffset FirstSeen { get; set; }
}
