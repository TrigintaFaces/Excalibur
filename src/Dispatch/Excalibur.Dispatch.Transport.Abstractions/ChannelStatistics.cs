// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents statistics for a message channel.
/// </summary>
public sealed class ChannelStatistics
{
	/// <summary>
	/// Gets or sets the current number of messages in the channel.
	/// </summary>
	/// <value>The current <see cref="CurrentCount"/> value.</value>
	public int CurrentCount { get; set; }

	/// <summary>
	/// Gets or sets the maximum capacity of the channel.
	/// </summary>
	/// <value>The current <see cref="Capacity"/> value.</value>
	public int Capacity { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages written to the channel.
	/// </summary>
	/// <value>The current <see cref="TotalWritten"/> value.</value>
	public long TotalWritten { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages read from the channel.
	/// </summary>
	/// <value>The current <see cref="TotalRead"/> value.</value>
	public long TotalRead { get; set; }

	/// <summary>
	/// Gets or sets the number of times the channel was full.
	/// </summary>
	/// <value>The current <see cref="FullCount"/> value.</value>
	public long FullCount { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the channel is currently closed for writing.
	/// </summary>
	/// <value>The current <see cref="IsWriterCompleted"/> value.</value>
	public bool IsWriterCompleted { get; set; }

	/// <summary>
	/// Gets the channel utilization percentage.
	/// </summary>
	/// <value>
	/// The channel utilization percentage.
	/// </value>
	public double UtilizationPercentage => Capacity > 0 ? (double)CurrentCount / Capacity * 100 : 0;
}
