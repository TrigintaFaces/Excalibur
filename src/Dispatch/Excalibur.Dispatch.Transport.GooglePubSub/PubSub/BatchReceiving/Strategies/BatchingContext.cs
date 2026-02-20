// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Context information for batching decisions.
/// </summary>
public sealed class BatchingContext
{
	/// <summary>
	/// Gets or sets the current queue depth (messages waiting).
	/// </summary>
	/// <value>
	/// The current queue depth (messages waiting).
	/// </value>
	public int QueueDepth { get; set; }

	/// <summary>
	/// Gets or sets the current processing rate (messages/second).
	/// </summary>
	/// <value>
	/// The current processing rate (messages/second).
	/// </value>
	public double ProcessingRate { get; set; }

	/// <summary>
	/// Gets or sets the current memory pressure (0-1).
	/// </summary>
	/// <value>
	/// The current memory pressure (0-1).
	/// </value>
	public double MemoryPressure { get; set; }

	/// <summary>
	/// Gets or sets the average message size in bytes.
	/// </summary>
	/// <value>
	/// The average message size in bytes.
	/// </value>
	public double AverageMessageSize { get; set; }

	/// <summary>
	/// Gets or sets the current flow control quota.
	/// </summary>
	/// <value>
	/// The current flow control quota.
	/// </value>
	public int FlowControlQuota { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the context.
	/// </summary>
	/// <value>
	/// The timestamp of the context.
	/// </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
