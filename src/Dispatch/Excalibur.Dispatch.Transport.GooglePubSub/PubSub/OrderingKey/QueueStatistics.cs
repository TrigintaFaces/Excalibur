// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics for a single ordering key queue.
/// </summary>
public sealed class QueueStatistics
{
	/// <summary>
	/// Gets the ordering key.
	/// </summary>
	/// <value>
	/// The ordering key.
	/// </value>
	public string OrderingKey { get; init; } = string.Empty;

	/// <summary>
	/// Gets the current queue depth.
	/// </summary>
	/// <value>
	/// The current queue depth.
	/// </value>
	public int QueueDepth { get; init; }

	/// <summary>
	/// Gets a value indicating whether gets or sets whether the queue is currently being processed.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the queue is currently being processed.
	/// </value>
	public bool IsProcessing { get; init; }

	/// <summary>
	/// Gets the number of messages processed.
	/// </summary>
	/// <value>
	/// The number of messages processed.
	/// </value>
	public long ProcessedCount { get; init; }

	/// <summary>
	/// Gets the number of errors encountered.
	/// </summary>
	/// <value>
	/// The number of errors encountered.
	/// </value>
	public long ErrorCount { get; init; }
}
