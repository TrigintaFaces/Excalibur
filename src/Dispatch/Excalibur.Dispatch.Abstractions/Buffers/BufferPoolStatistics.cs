// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Statistics for the buffer pool.
/// </summary>
public sealed class BufferPoolStatistics
{
	/// <summary>
	/// Gets the total number of buffer allocations performed by the pool.
	/// </summary>
	/// <value> The cumulative count of buffer rentals. </value>
	public long TotalAllocations { get; init; }

	/// <summary>
	/// Gets the total number of buffer deallocations performed by the pool.
	/// </summary>
	/// <value> The cumulative count of buffer returns. </value>
	public long TotalDeallocations { get; init; }

	/// <summary>
	/// Gets the total number of bytes rented from the buffer pool.
	/// </summary>
	/// <value> The aggregate number of bytes provided by the pool. </value>
	public long TotalBytesRented { get; init; }

	/// <summary>
	/// Gets the total number of bytes returned to the buffer pool.
	/// </summary>
	/// <value> The aggregate number of bytes returned to the pool. </value>
	public long TotalBytesReturned { get; init; }

	/// <summary>
	/// Gets the current number of buffers currently in use (rented but not returned).
	/// </summary>
	/// <value> The number of buffers that are currently rented out. </value>
	public int BuffersInUse { get; init; }

	/// <summary>
	/// Gets the peak number of buffers that were simultaneously in use.
	/// </summary>
	/// <value> The maximum number of concurrent buffer rentals. </value>
	public long PeakBuffersInUse { get; init; }

	/// <summary>
	/// Gets the statistics for individual buckets within the buffer pool.
	/// </summary>
	/// <value> The per-bucket metrics describing pool utilization. </value>
	public BucketStatistics[] BucketStatistics { get; init; } = [];
}
