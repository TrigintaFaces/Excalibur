// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents a snapshot of queue metrics at a point in time.
/// </summary>
public sealed record QueueMetricsSnapshot
{
	/// <summary>
	/// Gets the total number of messages processed.
	/// </summary>
	/// <value>
	/// The total number of messages processed.
	/// </value>
	public long TotalMessagesProcessed { get; init; }

	/// <summary>
	/// Gets the total number of successful message processing operations.
	/// </summary>
	/// <value>
	/// The total number of successful message processing operations.
	/// </value>
	public long SuccessfulMessages { get; init; }

	/// <summary>
	/// Gets the total number of failed message processing operations.
	/// </summary>
	/// <value>
	/// The total number of failed message processing operations.
	/// </value>
	public long FailedMessages { get; init; }

	/// <summary>
	/// Gets the average message processing time in milliseconds.
	/// </summary>
	/// <value>
	/// The average message processing time in milliseconds.
	/// </value>
	public double AverageProcessingTimeMs { get; init; }

	/// <summary>
	/// Gets the total number of batches processed.
	/// </summary>
	/// <value>
	/// The total number of batches processed.
	/// </value>
	public long TotalBatchesProcessed { get; init; }

	/// <summary>
	/// Gets the average batch size.
	/// </summary>
	/// <value>
	/// The average batch size.
	/// </value>
	public double AverageBatchSize { get; init; }

	/// <summary>
	/// Gets the total number of receive operations.
	/// </summary>
	/// <value>
	/// The total number of receive operations.
	/// </value>
	public long TotalReceiveOperations { get; init; }

	/// <summary>
	/// Gets the average receive time in milliseconds.
	/// </summary>
	/// <value>
	/// The average receive time in milliseconds.
	/// </value>
	public double AverageReceiveTimeMs { get; init; }

	/// <summary>
	/// Gets the total number of delete operations.
	/// </summary>
	/// <value>
	/// The total number of delete operations.
	/// </value>
	public long TotalDeleteOperations { get; init; }

	/// <summary>
	/// Gets the number of successful delete operations.
	/// </summary>
	/// <value>
	/// The number of successful delete operations.
	/// </value>
	public long SuccessfulDeletes { get; init; }

	/// <summary>
	/// Gets the total number of visibility timeout updates.
	/// </summary>
	/// <value>
	/// The total number of visibility timeout updates.
	/// </value>
	public long TotalVisibilityUpdates { get; init; }

	/// <summary>
	/// Gets the number of successful visibility timeout updates.
	/// </summary>
	/// <value>
	/// The number of successful visibility timeout updates.
	/// </value>
	public long SuccessfulVisibilityUpdates { get; init; }

	/// <summary>
	/// Gets the timestamp when the snapshot was taken.
	/// </summary>
	/// <value>
	/// The timestamp when the snapshot was taken.
	/// </value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
