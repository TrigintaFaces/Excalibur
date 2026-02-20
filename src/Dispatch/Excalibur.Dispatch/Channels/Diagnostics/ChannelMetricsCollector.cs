// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels.Diagnostics;

/// <summary>
/// Performance counter for channel metrics.
/// </summary>
public sealed class ChannelMetricsCollector(string channelId)
{
	private readonly string _channelId = channelId ?? throw new ArgumentNullException(nameof(channelId));
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private long _totalWrites;
	private long _totalReads;
	private long _failedWrites;
	private long _failedReads;

	/// <summary>
	/// Gets the total number of successful write operations.
	/// </summary>
	/// <value>
	/// The total number of successful write operations.
	/// </value>
	public long TotalWrites => Interlocked.Read(ref _totalWrites);

	/// <summary>
	/// Gets the total number of successful read operations.
	/// </summary>
	/// <value>
	/// The total number of successful read operations.
	/// </value>
	public long TotalReads => Interlocked.Read(ref _totalReads);

	/// <summary>
	/// Gets the total number of failed write operations.
	/// </summary>
	/// <value>
	/// The total number of failed write operations.
	/// </value>
	public long FailedWrites => Interlocked.Read(ref _failedWrites);

	/// <summary>
	/// Gets the total number of failed read operations.
	/// </summary>
	/// <value>
	/// The total number of failed read operations.
	/// </value>
	public long FailedReads => Interlocked.Read(ref _failedReads);

	/// <summary>
	/// Gets the peak number of items ever held in the channel.
	/// </summary>
	/// <value>The current <see cref="PeakCount"/> value.</value>
	public int PeakCount { get; private set; }

	/// <summary>
	/// Records a write operation with its success status and current channel count.
	/// </summary>
	/// <param name="success"> Whether the write operation was successful. </param>
	/// <param name="currentCount"> The current number of items in the channel after the write. </param>
	public void RecordWrite(bool success, int currentCount)
	{
		lock (_lock)
		{
			if (success)
			{
				_ = Interlocked.Increment(ref _totalWrites);
				if (currentCount > PeakCount)
				{
					PeakCount = currentCount;
				}
			}
			else
			{
				_ = Interlocked.Increment(ref _failedWrites);
			}
		}
	}

	/// <summary>
	/// Records a read operation with its success status.
	/// </summary>
	/// <param name="success"> Whether the read operation was successful. </param>
	public void RecordRead(bool success) => _ = success ? Interlocked.Increment(ref _totalReads) : Interlocked.Increment(ref _failedReads);

	/// <summary>
	/// Publishes the current channel statistics to the event source.
	/// </summary>
	/// <param name="currentCount"> The current number of items in the channel. </param>
	public void PublishStatistics(int currentCount) =>
		ChannelEventSource.Log.ChannelStatistics(
			_channelId,
			_totalWrites,
			_totalReads,
			_failedWrites,
			_failedReads,
			currentCount,
			PeakCount);
}
