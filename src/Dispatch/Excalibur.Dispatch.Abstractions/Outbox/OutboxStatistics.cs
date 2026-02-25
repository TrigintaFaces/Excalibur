// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides statistics about the outbox store usage and performance.
/// </summary>
public sealed class OutboxStatistics
{
	/// <summary>
	/// Gets the number of messages staged and awaiting delivery.
	/// </summary>
	/// <value> The count of staged messages. </value>
	public int StagedMessageCount { get; init; }

	/// <summary>
	/// Gets the number of messages currently being sent.
	/// </summary>
	/// <value> The count of in-flight send operations. </value>
	public int SendingMessageCount { get; init; }

	/// <summary>
	/// Gets the number of successfully sent messages.
	/// </summary>
	/// <value> The count of successfully dispatched messages. </value>
	public int SentMessageCount { get; init; }

	/// <summary>
	/// Gets the number of messages that failed delivery.
	/// </summary>
	/// <value> The count of failed messages. </value>
	public int FailedMessageCount { get; init; }

	/// <summary>
	/// Gets the number of messages scheduled for future delivery.
	/// </summary>
	/// <value> The count of scheduled messages. </value>
	public int ScheduledMessageCount { get; init; }

	/// <summary>
	/// Gets the age of the oldest unsent message.
	/// </summary>
	/// <value> The age of the oldest pending message, or <see langword="null" /> when none exist. </value>
	public TimeSpan? OldestUnsentMessageAge { get; init; }

	/// <summary>
	/// Gets the age of the oldest failed message.
	/// </summary>
	/// <value> The age of the oldest failed message, or <see langword="null" /> when none exist. </value>
	public TimeSpan? OldestFailedMessageAge { get; init; }

	/// <summary>
	/// Gets the timestamp when these statistics were captured.
	/// </summary>
	/// <value> The capture timestamp. </value>
	public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the total number of messages in the outbox.
	/// </summary>
	/// <value> The aggregate message count. </value>
	public int TotalMessageCount =>
		StagedMessageCount + SendingMessageCount + SentMessageCount + FailedMessageCount + ScheduledMessageCount;

	/// <inheritdoc />
	public override string ToString() =>
		$"OutboxStats: {TotalMessageCount} total ({StagedMessageCount} staged, {SentMessageCount} sent, {FailedMessageCount} failed)";
}
