// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// High-performance buffer pool for message processing that minimizes allocations.
/// </summary>
/// <remarks>
/// This pool is optimized for message processing scenarios where buffers of various sizes are frequently allocated and deallocated. It uses
/// ArrayPool internally with custom sizing logic optimized for typical message sizes.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="MessageBufferPool" /> class. </remarks>
/// <param name="maxArrayLength"> Maximum array length that can be rented from the pool. </param>
/// <param name="maxArraysPerBucket"> Maximum number of arrays per bucket in the pool. </param>
public sealed class MessageBufferPool(int maxArrayLength = 1024 * 1024, int maxArraysPerBucket = 50)
{
	/// <summary>
	/// Default buffer sizes optimized for typical message processing scenarios.
	/// </summary>
	private static readonly int[] DefaultBufferSizes = [512, 1024, 4096, 16384, 65536, 131072];

	private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create(maxArrayLength, maxArraysPerBucket);
	private long _totalRented;
	private long _totalReturned;

	/// <summary>
	/// Gets the shared instance of the message buffer pool.
	/// </summary>
	/// <value> The shared instance of the message buffer pool. </value>
	public static MessageBufferPool Shared { get; } = new();

	/// <summary>
	/// Gets the total number of buffers rented from this pool.
	/// </summary>
	/// <value> The total number of buffers rented from this pool. </value>
	public long TotalRented => Volatile.Read(ref _totalRented);

	/// <summary>
	/// Gets the total number of buffers returned to this pool.
	/// </summary>
	/// <value> The total number of buffers returned to this pool. </value>
	public long TotalReturned => Volatile.Read(ref _totalReturned);

	/// <summary>
	/// Gets the number of buffers currently in use.
	/// </summary>
	/// <value> The current <see cref="CurrentlyInUse" /> value. </value>
	public long CurrentlyInUse => TotalRented - TotalReturned;

	/// <summary>
	/// Gets the optimal buffer size for a given minimum size.
	/// </summary>
	/// <param name="minimumSize"> The minimum size required. </param>
	/// <returns> The optimal buffer size to rent. </returns>
	public static int GetOptimalBufferSize(int minimumSize)
	{
		// Find the smallest standard size that fits
		foreach (var size in DefaultBufferSizes)
		{
			if (size >= minimumSize)
			{
				return size;
			}
		}

		// For larger sizes, round up to the nearest power of 2
		return (int)BitOperations.RoundUpToPowerOf2((uint)minimumSize);
	}

	/// <summary>
	/// Rents a buffer from the pool.
	/// </summary>
	/// <param name="minimumLength"> The minimum length of the buffer required. </param>
	/// <returns> A rented buffer wrapper that must be disposed to return the buffer to the pool. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public RentedBuffer Rent(int minimumLength)
	{
		if (minimumLength < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(minimumLength),
				Resources.MessageBufferPool_MinimumLengthNonNegative);
		}

		if (minimumLength > maxArrayLength)
		{
			throw new ArgumentOutOfRangeException(
				nameof(minimumLength),
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.MessageBufferPool_RequestExceedsMaxArrayLength,
					minimumLength,
					maxArrayLength));
		}

		var buffer = _arrayPool.Rent(minimumLength);
		_ = Interlocked.Increment(ref _totalRented);
		return new RentedBuffer(this, buffer, minimumLength);
	}

	/// <summary>
	/// Returns a buffer to the pool.
	/// </summary>
	/// <param name="buffer"> The buffer to return. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer before returning it. </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void Return(byte[] buffer, bool clearBuffer = true)
	{
		_arrayPool.Return(buffer, clearBuffer);
		_ = Interlocked.Increment(ref _totalReturned);
	}
}
