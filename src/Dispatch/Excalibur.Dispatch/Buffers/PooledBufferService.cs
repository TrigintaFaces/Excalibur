// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Thread-safe service for managing pooled buffers using ArrayPool.
/// </summary>
public sealed class PooledBufferService : IPooledBufferService
{
	private readonly ArrayPool<byte> _pool;
	private readonly bool _clearBuffersByDefault;
	private int _rentedBuffers;
	private long _totalRentOperations;
	private long _totalReturnOperations;
	private int _largestBufferRequested;

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBufferService"/> class.
	/// Initializes a new instance of <see cref="PooledBufferService" /> with default settings.
	/// </summary>
	public PooledBufferService()
	{
		_pool = ArrayPool<byte>.Create(1024 * 1024, 50); // 1MB default, 50 arrays per bucket
		_clearBuffersByDefault = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBufferService"/> class.
	/// Initializes a new instance of <see cref="PooledBufferService" />.
	/// </summary>
	/// <param name="maxArrayLength"> The maximum length of array instances that may be stored in the pool. </param>
	/// <param name="maxArraysPerBucket"> The maximum number of array instances that may be stored in each bucket in the pool. </param>
	/// <param name="clearBuffersByDefault"> Whether to clear buffers by default when returning to pool. </param>
	public PooledBufferService(int maxArrayLength, int maxArraysPerBucket, bool clearBuffersByDefault)
	{
		_pool = ArrayPool<byte>.Create(maxArrayLength, maxArraysPerBucket);
		_clearBuffersByDefault = clearBuffersByDefault;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBufferService"/> class.
	/// Uses the shared ArrayPool instance with default clearing enabled.
	/// </summary>
	public PooledBufferService(bool useShared)
	{
		if (useShared)
		{
			_pool = ArrayPool<byte>.Shared;
		}
		else
		{
			_pool = ArrayPool<byte>.Create(1024 * 1024, 50);
		}

		_clearBuffersByDefault = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBufferService"/> class.
	/// Uses the shared ArrayPool instance.
	/// </summary>
	/// <param name="useShared"> Whether to use the shared ArrayPool instance. </param>
	/// <param name="clearBuffersByDefault"> Whether to clear buffers by default when returning to pool. </param>
	public PooledBufferService(bool useShared, bool clearBuffersByDefault)
	{
		if (useShared)
		{
			_pool = ArrayPool<byte>.Shared;
		}
		else
		{
			_pool = ArrayPool<byte>.Create(1024 * 1024, 50);
		}

		_clearBuffersByDefault = clearBuffersByDefault;
	}

	/// <summary>
	/// Gets the shared instance of pooled buffer service.
	/// </summary>
	/// <value>
	/// The shared instance of pooled buffer service.
	/// </value>
	public static IPooledBufferService Shared { get; } = new PooledBufferService(useShared: true, clearBuffersByDefault: true);

	/// <inheritdoc />
	public int RentedBuffers => _rentedBuffers;

	/// <inheritdoc />
	public long TotalRentOperations => Interlocked.Read(ref _totalRentOperations);

	/// <inheritdoc />
	public long TotalReturnOperations => Interlocked.Read(ref _totalReturnOperations);

	/// <inheritdoc />
	public int LargestBufferRequested => _largestBufferRequested;

	/// <inheritdoc />
	IDisposablePooledBuffer IPooledBufferService.RentBuffer(int minimumLength, bool clearBuffer)
	{
		var buffer = RentBufferCore(minimumLength, clearBuffer);
		return new PooledBuffer(this, buffer);
	}

	/// <inheritdoc />
	void IPooledBufferService.ReturnBuffer(IPooledBuffer pooledBuffer, bool clearBuffer)
	{
		if (pooledBuffer is PooledBuffer pb)
		{
			ReturnBuffer(pb.Buffer, clearBuffer);
		}
	}

	/// <summary>
	/// Rents a buffer for backward compatibility.
	/// </summary>
	public byte[] RentBuffer(int minimumLength, bool clearBuffer = false) => RentBufferCore(minimumLength, clearBuffer);

	/// <inheritdoc />
	public void ReturnBuffer(byte[] buffer, bool clearBuffer = true)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		_ = Interlocked.Decrement(ref _rentedBuffers);
		_ = Interlocked.Increment(ref _totalReturnOperations);

		// Use the class default if not explicitly specified
		var shouldClear = clearBuffer || _clearBuffersByDefault;

		_pool.Return(buffer, shouldClear);
	}

	/// <summary>
	/// Gets buffer usage statistics.
	/// </summary>
	public BufferPoolStatistics GetStatistics() =>
		new()
		{
			TotalAllocations = _totalRentOperations,
			TotalDeallocations = _totalReturnOperations,
			BuffersInUse = _rentedBuffers,
			PeakBuffersInUse = _largestBufferRequested,
		};

	/// <summary>
	/// Core implementation of buffer renting.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	private byte[] RentBufferCore(int minimumLength, bool clearBuffer = false)
	{
		if (minimumLength < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minimumLength), ErrorMessages.MinimumLengthCannotBeNegative);
		}

		var buffer = _pool.Rent(minimumLength);

		_ = Interlocked.Increment(ref _rentedBuffers);
		_ = Interlocked.Increment(ref _totalRentOperations);

		// Update largest buffer requested
		int currentLargest;
		do
		{
			currentLargest = _largestBufferRequested;
			if (minimumLength <= currentLargest)
			{
				break;
			}
		}
		while (Interlocked.CompareExchange(ref _largestBufferRequested, minimumLength, currentLargest) != currentLargest);

		if (clearBuffer && buffer.Length > 0)
		{
			Array.Clear(buffer, 0, buffer.Length);
		}

		return buffer;
	}
}
