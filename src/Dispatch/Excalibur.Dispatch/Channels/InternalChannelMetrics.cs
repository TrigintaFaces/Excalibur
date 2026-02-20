// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Channel metrics collector.
/// </summary>
public sealed class InternalChannelMetrics(string channelName, int capacity)
{
	// R0.8: Avoid unused private fields - these are kept for future use
#pragma warning disable CA1823, IDE0052
	private long _totalReads;
#pragma warning restore CA1823, IDE0052
	private long _totalWrites;

	/// <summary>
	/// Gets the total number of read operations performed.
	/// </summary>
	/// <value>
	/// The total number of read operations performed.
	/// </value>
	public long TotalReads => Interlocked.Read(ref _totalReads);

	/// <summary>
	/// Gets the total number of write operations performed.
	/// </summary>
	/// <value>
	/// The total number of write operations performed.
	/// </value>
	public long TotalWrites => Interlocked.Read(ref _totalWrites);

	/// <summary>
	/// Records a read operation.
	/// </summary>
	public void RecordRead() => _ = Interlocked.Increment(ref _totalReads);

	/// <summary>
	/// Records a write operation.
	/// </summary>
	public void RecordWrite() => _ = Interlocked.Increment(ref _totalWrites);

	/// <summary>
	/// Resets all metrics to zero.
	/// </summary>
	public void Reset()
	{
		_ = Interlocked.Exchange(ref _totalReads, 0);
		_ = Interlocked.Exchange(ref _totalWrites, 0);
	}
}
