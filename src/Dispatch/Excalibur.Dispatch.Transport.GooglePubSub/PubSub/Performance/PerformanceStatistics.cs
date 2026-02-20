// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Performance statistics for Google Cloud Pub/Sub message processing.
/// </summary>
public sealed class PerformanceStatistics
{
	/// <summary>
	/// Gets the total number of messages enqueued.
	/// </summary>
	/// <value>
	/// The total number of messages enqueued.
	/// </value>
	public long MessagesEnqueued { get; init; }

	/// <summary>
	/// Gets the total number of messages processed successfully.
	/// </summary>
	/// <value>
	/// The total number of messages processed successfully.
	/// </value>
	public long MessagesProcessed { get; init; }

	/// <summary>
	/// Gets the total number of messages that failed processing.
	/// </summary>
	/// <value>
	/// The total number of messages that failed processing.
	/// </value>
	public long MessagesFailed { get; init; }

	/// <summary>
	/// Gets the average time messages spend in queue.
	/// </summary>
	/// <value>
	/// The average time messages spend in queue.
	/// </value>
	public TimeSpan AverageQueueTime { get; init; }

	/// <summary>
	/// Gets the 95th percentile queue time.
	/// </summary>
	/// <value>
	/// The 95th percentile queue time.
	/// </value>
	public TimeSpan P95QueueTime { get; init; }

	/// <summary>
	/// Gets the average message processing time.
	/// </summary>
	/// <value>
	/// The average message processing time.
	/// </value>
	public TimeSpan AverageProcessingTime { get; init; }

	/// <summary>
	/// Gets the 95th percentile processing time.
	/// </summary>
	/// <value>
	/// The 95th percentile processing time.
	/// </value>
	public TimeSpan P95ProcessingTime { get; init; }
}
