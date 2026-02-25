// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a point-in-time snapshot of flow control metrics.
/// </summary>
public sealed class FlowControlMetricsSnapshot
{
	/// <summary>
	/// Gets or sets the total number of messages received.
	/// </summary>
	/// <value>
	/// The total number of messages received.
	/// </value>
	public long MessagesReceived { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages processed.
	/// </summary>
	/// <value>
	/// The total number of messages processed.
	/// </value>
	public long MessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the total bytes received.
	/// </summary>
	/// <value>
	/// The total bytes received.
	/// </value>
	public long BytesReceived { get; set; }

	/// <summary>
	/// Gets or sets the total bytes processed.
	/// </summary>
	/// <value>
	/// The total bytes processed.
	/// </value>
	public long BytesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the number of processing errors.
	/// </summary>
	/// <value>
	/// The number of processing errors.
	/// </value>
	public long ProcessingErrors { get; set; }

	/// <summary>
	/// Gets or sets the number of flow control pauses.
	/// </summary>
	/// <value>
	/// The number of flow control pauses.
	/// </value>
	public long FlowControlPauses { get; set; }

	/// <summary>
	/// Gets or sets the current outstanding message count.
	/// </summary>
	/// <value>
	/// The current outstanding message count.
	/// </value>
	public long CurrentOutstandingMessages { get; set; }

	/// <summary>
	/// Gets or sets the current outstanding byte count.
	/// </summary>
	/// <value>
	/// The current outstanding byte count.
	/// </value>
	public long CurrentOutstandingBytes { get; set; }

	/// <summary>
	/// Gets or sets the message processing rate.
	/// </summary>
	/// <value>
	/// The message processing rate.
	/// </value>
	public double MessageProcessingRate { get; set; }

	/// <summary>
	/// Gets or sets the byte processing rate.
	/// </summary>
	/// <value>
	/// The byte processing rate.
	/// </value>
	public double ByteProcessingRate { get; set; }

	/// <summary>
	/// Gets or sets the error rate.
	/// </summary>
	/// <value>
	/// The error rate.
	/// </value>
	public double ErrorRate { get; set; }

	/// <summary>
	/// Gets or sets the utilization percentage.
	/// </summary>
	/// <value>
	/// The utilization percentage.
	/// </value>
	public double UtilizationPercentage { get; set; }

	/// <summary>
	/// Gets or sets the time when this snapshot was taken.
	/// </summary>
	/// <value>
	/// The time when this snapshot was taken.
	/// </value>
	public DateTimeOffset SnapshotTime { get; set; }
}
