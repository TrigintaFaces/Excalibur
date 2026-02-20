// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides statistics about outbox publisher performance.
/// </summary>
public sealed class PublisherStatistics
{
	/// <summary>
	/// Gets the total number of publishing operations performed.
	/// </summary>
	/// <value> The current <see cref="TotalOperations" /> value. </value>
	public long TotalOperations { get; init; }

	/// <summary>
	/// Gets the total number of messages successfully published.
	/// </summary>
	/// <value> The current <see cref="TotalMessagesPublished" /> value. </value>
	public long TotalMessagesPublished { get; init; }

	/// <summary>
	/// Gets the total number of messages that failed to publish.
	/// </summary>
	/// <value> The current <see cref="TotalMessagesFailed" /> value. </value>
	public long TotalMessagesFailed { get; init; }

	/// <summary>
	/// Gets the average publishing rate in messages per second.
	/// </summary>
	/// <value> The current <see cref="AverageMessagesPerSecond" /> value. </value>
	public double AverageMessagesPerSecond { get; init; }

	/// <summary>
	/// Gets the current success rate as a percentage.
	/// </summary>
	/// <value> The current <see cref="CurrentSuccessRate" /> value. </value>
	public double CurrentSuccessRate { get; init; }

	/// <summary>
	/// Gets the timestamp of the last publishing operation.
	/// </summary>
	/// <value> The current <see cref="LastOperationAt" /> value. </value>
	public DateTimeOffset? LastOperationAt { get; init; }

	/// <summary>
	/// Gets the duration of the last publishing operation.
	/// </summary>
	/// <value> The current <see cref="LastOperationDuration" /> value. </value>
	public TimeSpan? LastOperationDuration { get; init; }

	/// <summary>
	/// Gets the timestamp when these statistics were captured.
	/// </summary>
	/// <value> The current <see cref="CapturedAt" /> value. </value>
	public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public override string ToString() =>
		$"PublisherStats: {TotalMessagesPublished} published, {TotalMessagesFailed} failed ({CurrentSuccessRate:F1}% success), {AverageMessagesPerSecond:F1} msg/sec";
}
