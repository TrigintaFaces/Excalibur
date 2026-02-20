// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about the streaming pull operations.
/// </summary>
public sealed class StreamingPullStatistics
{
	/// <summary>
	/// Gets or sets the number of active streams.
	/// </summary>
	/// <value>
	/// The number of active streams.
	/// </value>
	public int ActiveStreamCount { get; set; }

	/// <summary>
	/// Gets or sets the target number of streams.
	/// </summary>
	/// <value>
	/// The target number of streams.
	/// </value>
	public int TargetStreamCount { get; set; }

	/// <summary>
	/// Gets or sets the total messages received.
	/// </summary>
	/// <value>
	/// The total messages received.
	/// </value>
	public long TotalMessagesReceived { get; set; }

	/// <summary>
	/// Gets or sets the total bytes received.
	/// </summary>
	/// <value>
	/// The total bytes received.
	/// </value>
	public long TotalBytesReceived { get; set; }

	/// <summary>
	/// Gets or sets the total number of errors.
	/// </summary>
	/// <value>
	/// The total number of errors.
	/// </value>
	public long TotalErrors { get; set; }

	/// <summary>
	/// Gets or sets the number of queued messages.
	/// </summary>
	/// <value>
	/// The number of queued messages.
	/// </value>
	public int QueuedMessages { get; set; }

	/// <summary>
	/// Gets or sets the number of active processing threads.
	/// </summary>
	/// <value>
	/// The number of active processing threads.
	/// </value>
	public int ActiveProcessingThreads { get; set; }

	/// <summary>
	/// Gets or sets the stream health information.
	/// </summary>
	/// <value>
	/// The stream health information.
	/// </value>
	public StreamHealthInfo[] StreamHealthInfos { get; set; } = [];
}
