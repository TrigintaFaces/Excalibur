// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Service for managing pools of message objects to reduce allocations in high-throughput scenarios.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessagePoolService" /> class. </remarks>
/// <param name="maxPoolSize"> Maximum size for each type-specific pool. </param>
/// <param name="trackMetrics"> Whether to track detailed metrics. </param>
public sealed class MessagePoolService(int maxPoolSize = 0, bool trackMetrics = false)
{
	private readonly ConcurrentDictionary<Type, object> _pools = new();
	private readonly int _maxPoolSize = maxPoolSize > 0 ? maxPoolSize : Environment.ProcessorCount * 4;

	/// <summary>
	/// Rents a message object of the specified type from the pool.
	/// </summary>
	/// <typeparam name="T"> The type of message to rent. </typeparam>
	/// <returns> A message instance from the pool or a new instance if the pool is empty. </returns>
	public T RentMessage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
		where T : class, IDispatchMessage, new()
	{
		var pool = GetOrCreatePool<T>();
		return pool.Get();
	}

	/// <summary>
	/// Returns a message object to the pool.
	/// </summary>
	/// <typeparam name="T"> The type of message to return. </typeparam>
	/// <param name="message"> The message to return to the pool. </param>
	public void ReturnMessage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T message)
		where T : class, IDispatchMessage, new()
	{
		var pool = GetOrCreatePool<T>();
		pool.Return(message);
	}

	/// <summary>
	/// Gets metrics for all pools.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicMethods' in call to target method",
		Justification = "Pool types are preserved through DI registration and known patterns")]
	public Dictionary<Type, PoolMetrics> GetAllMetrics()
	{
		var metrics = new Dictionary<Type, PoolMetrics>();

		foreach (var kvp in _pools)
		{
			if (kvp.Value is ObjectPool<IDispatchMessage>)
			{
				// Use reflection to call GetMetrics on the concrete type
				var getMetricsMethod = kvp.Value.GetType().GetMethod(nameof(MessageObjectPool<>.GetMetrics));
				if (getMetricsMethod != null)
				{
					var poolMetrics = (PoolMetrics)getMetricsMethod.Invoke(kvp.Value, parameters: null)!;
					metrics[kvp.Key] = poolMetrics;
				}
			}
		}

		return metrics;
	}

	/// <summary>
	/// Clears all pools, releasing all pooled objects.
	/// </summary>
	public void Clear() => _pools.Clear();

	/// <summary>
	/// Gets the pool for the specified message type.
	/// </summary>
	/// <typeparam name="T"> The type of message. </typeparam>
	/// <returns> The object pool for the specified message type. </returns>
	public ObjectPool<T> GetPool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
		where T : class, IDispatchMessage, new() => GetOrCreatePool<T>();

	/// <summary>
	/// Gets or creates a pool for the specified message type.
	/// </summary>
	private ObjectPool<T> GetOrCreatePool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
		where T : class, IDispatchMessage, new() =>
		(ObjectPool<T>)_pools.GetOrAdd(
			typeof(T),
			(_, self) => self.CreatePool<T>(),
			this);

	/// <summary>
	/// Creates a new pool for the specified message type.
	/// </summary>
	private MessageObjectPool<T> CreatePool<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
		where T : class, IDispatchMessage, new()
	{
		var policy = new MessagePooledObjectPolicy<T>();
		return new MessageObjectPool<T>(policy, _maxPoolSize, preWarm: 0, trackMetrics);
	}
}
