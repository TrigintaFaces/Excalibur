// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// A high-performance, thread-safe object pool implementation.
/// </summary>
/// <typeparam name="T"> The type of objects to pool. </typeparam>
public sealed class MessageObjectPool<T> : ObjectPool<T>
	where T : class
{
	private readonly ConcurrentBag<T> _pool = [];
	private readonly IPooledObjectPolicy<T> _policy;
	private readonly int _maxSize;
	private readonly bool _trackMetrics;
	private int _totalCreated;

	/// <summary>
	/// Performance metrics.
	/// </summary>
	private long _rentCount;

	private long _returnCount;
	private long _createCount;
	private long _discardCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageObjectPool{T}" /> class.
	/// </summary>
	/// <param name="policy"> The policy for creating and resetting objects. </param>
	/// <param name="maxSize"> Maximum number of objects to retain in the pool. Default is Environment.ProcessorCount * 2. </param>
	/// <param name="preWarm"> Number of objects to pre-create. Default is 0. </param>
	/// <param name="trackMetrics"> Whether to track detailed metrics. Default is false. </param>
	public MessageObjectPool(
		IPooledObjectPolicy<T> policy,
		int maxSize = 0,
		int preWarm = 0,
		bool trackMetrics = false)
	{
		_policy = policy ?? throw new ArgumentNullException(nameof(policy));
		_maxSize = maxSize > 0 ? maxSize : Environment.ProcessorCount * 2;
		_trackMetrics = trackMetrics;

		// Pre-warm the pool
		for (var i = 0; i < Math.Min(preWarm, _maxSize); i++)
		{
			var item = _policy.Create();
			_pool.Add(item);
			_ = Interlocked.Increment(ref _totalCreated);
		}
	}

	/// <inheritdoc />
	public int AvailableCount => _pool.Count;

	/// <inheritdoc />
	public int MaximumPoolSize => _maxSize;

	/// <inheritdoc />
	public int TotalCreated => _totalCreated;

	/// <summary>
	/// Gets the total number of times an object was rented from the pool.
	/// </summary>
	/// <value>The current <see cref="RentCount"/> value.</value>
	public long RentCount => _rentCount;

	/// <summary>
	/// Gets the total number of times an object was returned to the pool.
	/// </summary>
	/// <value>The current <see cref="ReturnCount"/> value.</value>
	public long ReturnCount => _returnCount;

	/// <summary>
	/// Gets the total number of times a new object was created.
	/// </summary>
	/// <value>The current <see cref="CreateCount"/> value.</value>
	public long CreateCount => _createCount;

	/// <summary>
	/// Gets the total number of times an object was discarded instead of returned to the pool.
	/// </summary>
	/// <value>The current <see cref="DiscardCount"/> value.</value>
	public long DiscardCount => _discardCount;

	/// <inheritdoc />
	public override T Get()
	{
		if (_trackMetrics)
		{
			_ = Interlocked.Increment(ref _rentCount);
		}

		if (_pool.TryTake(out var item))
		{
			return item;
		}

		// Pool is empty, create a new instance
		if (_trackMetrics)
		{
			_ = Interlocked.Increment(ref _createCount);
		}

		_ = Interlocked.Increment(ref _totalCreated);
		return _policy.Create();
	}

	/// <inheritdoc />
	public override void Return(T item)
	{
		if (item == null)
		{
			return;
		}

		if (_trackMetrics)
		{
			_ = Interlocked.Increment(ref _returnCount);
		}

		// Let the policy decide if the object should be returned
		if (!_policy.Return(item))
		{
			if (_trackMetrics)
			{
				_ = Interlocked.Increment(ref _discardCount);
			}

			return;
		}

		// Check pool size limit
		if (_pool.Count < _maxSize)
		{
			_pool.Add(item);
		}
		else if (_trackMetrics)
		{
			_ = Interlocked.Increment(ref _discardCount);
		}
	}

	/// <summary>
	/// Gets a snapshot of the current pool metrics.
	/// </summary>
	public PoolMetrics GetMetrics() =>
		new(
			AvailableCount,
			TotalCreated,
			_rentCount,
			_returnCount,
			_createCount,
			_discardCount);
}

/// <summary>
/// Snapshot of pool metrics at a point in time.
/// </summary>
/// <param name="AvailableCount">Number of objects currently available in the pool.</param>
/// <param name="TotalCreated">Total number of objects created by the pool.</param>
/// <param name="RentCount">Total number of rent operations.</param>
/// <param name="ReturnCount">Total number of return operations.</param>
/// <param name="CreateCount">Total number of objects created on demand (cache misses).</param>
/// <param name="DiscardCount">Total number of objects discarded instead of returned.</param>
public readonly record struct PoolMetrics(
	int AvailableCount,
	int TotalCreated,
	long RentCount,
	long ReturnCount,
	long CreateCount,
	long DiscardCount);
