// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// BCL-based buffer pool adapter that wraps System.Buffers.ArrayPool&lt;byte&gt; for optimal performance. Replaces custom buffer pooling
/// implementations with the highly-optimized BCL ArrayPool.
/// </summary>
/// <remarks> Initializes a new instance of the BCL buffer pool adapter. </remarks>
/// <param name="clearBuffers"> Whether to clear buffers on return (default: false for performance). </param>
/// <param name="trackAllocations"> Whether to track allocation statistics (default: false for performance). </param>
/// <param name="arrayPool"> The underlying ArrayPool to use (default: ArrayPool&lt;byte&gt;.Shared). </param>
public sealed class BclBufferPoolAdapter(
	bool clearBuffers = false,
	bool trackAllocations = false,
	ArrayPool<byte>? arrayPool = null) : IPooledBufferService
{
	private readonly ArrayPool<byte> _arrayPool = arrayPool ?? ArrayPool<byte>.Shared;

	/// <summary>
	/// Performance counters (only when tracking is enabled).
	/// </summary>
	private long _totalAllocations;

	private long _totalDeallocations;
	private long _totalBytesRented;
	private long _totalBytesReturned;
	private long _peakBuffersInUse;
	private int _buffersInUse;
	private int _largestBufferRequested;

	/// <summary>
	/// Gets the shared BCL buffer pool adapter instance (optimized for maximum performance).
	/// </summary>
	/// <value> The shared singleton instance backed by <see cref="ArrayPool{T}.Shared" />. </value>
	public static BclBufferPoolAdapter Shared { get; } = new(
		clearBuffers: false,
		trackAllocations: false,
		arrayPool: ArrayPool<byte>.Shared);

	/// <summary>
	/// Gets the number of buffers currently rented from the pool.
	/// </summary>
	/// <value> The count of buffers that have been rented but not returned. </value>
	public int RentedBuffers => _buffersInUse;

	/// <summary>
	/// Gets the total number of buffer rent operations performed.
	/// </summary>
	/// <value> The cumulative number of rent operations. </value>
	public long TotalRentOperations => Interlocked.Read(ref _totalAllocations);

	/// <summary>
	/// Gets the total number of buffer return operations performed.
	/// </summary>
	/// <value> The cumulative number of return operations. </value>
	public long TotalReturnOperations => Interlocked.Read(ref _totalDeallocations);

	/// <summary>
	/// Gets the largest buffer size that has been requested.
	/// </summary>
	/// <value> The maximum requested buffer length. </value>
	public int LargestBufferRequested => _largestBufferRequested;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] RentBuffer(int minimumLength, bool clearBuffer = false)
	{
		if (minimumLength < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(nameof(minimumLength));
		}

		// Use the BCL ArrayPool - it's already highly optimized with:
		// - Lock-free design for common sizes
		// - NUMA-aware allocation
		// - Size buckets optimized for real-world usage patterns
		// - Thread-local caching for hot paths
		var buffer = _arrayPool.Rent(minimumLength);

		// Update tracking metrics if enabled
		if (trackAllocations)
		{
			TrackRent(buffer.Length, minimumLength);
		}

		// Clear if requested (BCL ArrayPool may return dirty buffers)
		if (clearBuffer || clearBuffers)
		{
			buffer.AsSpan(0, Math.Min(minimumLength, buffer.Length)).Clear();
		}

		return buffer;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReturnBuffer(byte[] buffer, bool clearBuffer = false)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		// Update tracking metrics if enabled
		if (trackAllocations)
		{
			TrackReturn(buffer.Length);
		}

		// Return to the BCL ArrayPool
		_arrayPool.Return(buffer, clearBuffer || clearBuffers);
	}

	/// <inheritdoc />
	IDisposablePooledBuffer IPooledBufferService.RentBuffer(int minimumLength, bool clearBuffer)
	{
		var buffer = RentBuffer(minimumLength, clearBuffer);
		return new BclPooledBuffer(this, buffer);
	}

	/// <inheritdoc />
	void IPooledBufferService.ReturnBuffer(IPooledBuffer pooledBuffer, bool clearBuffer)
	{
		if (pooledBuffer is BclPooledBuffer bclBuffer)
		{
			ReturnBuffer(bclBuffer.Buffer, clearBuffer);
		}
	}

	/// <inheritdoc />
	public BufferPoolStatistics GetStatistics() =>
		new()
		{
			TotalAllocations = Interlocked.Read(ref _totalAllocations),
			TotalDeallocations = Interlocked.Read(ref _totalDeallocations),
			TotalBytesRented = Interlocked.Read(ref _totalBytesRented),
			TotalBytesReturned = Interlocked.Read(ref _totalBytesReturned),
			BuffersInUse = _buffersInUse,
			PeakBuffersInUse = Interlocked.Read(ref _peakBuffersInUse),

			// BCL ArrayPool doesn't expose bucket statistics - this is acceptable trade-off for better performance
			BucketStatistics =
			[
				new BucketStatistics
				{
					BucketName = "BCL ArrayPool",
					MaxSize = int.MaxValue,
					TotalRents = Interlocked.Read(ref _totalAllocations),
					TotalReturns = Interlocked.Read(ref _totalDeallocations),
				},
			],
		};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackRent(int actualBufferSize, int requestedSize)
	{
		_ = Interlocked.Increment(ref _totalAllocations);
		_ = Interlocked.Add(ref _totalBytesRented, actualBufferSize);

		var newCount = Interlocked.Increment(ref _buffersInUse);

		// Update peak usage
		long currentPeak;
		do
		{
			currentPeak = _peakBuffersInUse;
			if (newCount <= currentPeak)
			{
				break;
			}
		} while (Interlocked.CompareExchange(ref _peakBuffersInUse, newCount, currentPeak) != currentPeak);

		// Track largest buffer requested
		int currentLargest;
		do
		{
			currentLargest = _largestBufferRequested;
			if (requestedSize <= currentLargest)
			{
				break;
			}
		} while (Interlocked.CompareExchange(ref _largestBufferRequested, requestedSize, currentLargest) != currentLargest);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrackReturn(int bufferSize)
	{
		_ = Interlocked.Increment(ref _totalDeallocations);
		_ = Interlocked.Add(ref _totalBytesReturned, bufferSize);
		_ = Interlocked.Decrement(ref _buffersInUse);
	}

	/// <summary>
	/// Helper class for throwing exceptions without impacting inlining.
	/// </summary>
	private static class ThrowHelper
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void ThrowArgumentOutOfRangeException(string paramName) =>
			throw new ArgumentOutOfRangeException(paramName);
	}
}
