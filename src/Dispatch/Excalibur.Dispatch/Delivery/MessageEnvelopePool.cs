// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Thread-local message envelope pool implementation.
/// </summary>
public sealed class MessageEnvelopePool : IMessageEnvelopePool, IDisposable
{
	private readonly IMessagePool _messagePool;
	private readonly MessageEnvelopePoolOptions _options;
	private readonly ThreadLocal<LocalPool> _threadLocalPool;
	private long _totalRentals;
	private long _totalReturns;
	private long _poolHits;
	private long _poolMisses;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEnvelopePool" /> class.
	/// </summary>
	public MessageEnvelopePool(
		IMessagePool messagePool,
		MessageEnvelopePoolOptions? options = null)
	{
		_messagePool = messagePool ?? throw new ArgumentNullException(nameof(messagePool));
		_options = options ?? new MessageEnvelopePoolOptions();
		_threadLocalPool = new ThreadLocal<LocalPool>(() => new LocalPool(_options.ThreadLocalCacheSize), trackAllValues: true);
	}

	/// <inheritdoc />
	public MessageEnvelopeHandle<TMessage> Rent<TMessage>(TMessage message, in MessageMetadata metadata)
		where TMessage : class, IDispatchMessage
	{
		_ = Interlocked.Increment(ref _totalRentals);

		// Convert struct metadata to record metadata
		var recordMetadata = metadata.ToRecordMetadata();

		// For struct messages, we don't need pooling
		if (typeof(TMessage).IsValueType)
		{
			var envelope = new MessageEnvelope<TMessage>(message, recordMetadata);
			return new MessageEnvelopeHandle<TMessage>(envelope, default(TMessage), this);
		}

		// For reference types, try to get from thread-local cache first
		var localPool = _threadLocalPool.Value;
		if (localPool.TryRent<TMessage>(out var cachedEnvelope))
		{
			_ = Interlocked.Increment(ref _poolHits);
			var updatedEnvelope = cachedEnvelope.WithMetadata(recordMetadata);
			return new MessageEnvelopeHandle<TMessage>(updatedEnvelope, message, this);
		}

		_ = Interlocked.Increment(ref _poolMisses);
		var newEnvelope = new MessageEnvelope<TMessage>(message, recordMetadata);
		return new MessageEnvelopeHandle<TMessage>(newEnvelope, message, this);
	}

	/// <inheritdoc />
	public MessageEnvelopeHandle<TMessage> RentWithContext<TMessage>(TMessage message, IMessageContext context)
		where TMessage : class, IDispatchMessage
	{
		var metadata = MessageMetadata.FromContext(context);
		var recordMetadata = metadata.ToRecordMetadata();
		var envelope = new MessageEnvelope<TMessage>(message, recordMetadata, context);

		_ = Interlocked.Increment(ref _totalRentals);
		return new MessageEnvelopeHandle<TMessage>(envelope, typeof(TMessage).IsValueType ? default : message, this);
	}

	/// <inheritdoc />
	public MessageEnvelopePoolStats GetStats()
	{
		var threadLocalStats = Array.Empty<ThreadLocalPoolStats>();
		if (_threadLocalPool.IsValueCreated)
		{
			var pools = _threadLocalPool.Values;
			threadLocalStats = new ThreadLocalPoolStats[pools.Count];
			var i = 0;
			foreach (var pool in pools)
			{
				threadLocalStats[i++] = pool.GetStats();
			}
		}

		return new MessageEnvelopePoolStats
		{
			TotalRentals = _totalRentals,
			TotalReturns = _totalReturns,
			PoolHits = _poolHits,
			PoolMisses = _poolMisses,
			HitRate = _totalRentals > 0 ? (double)_poolHits / _totalRentals : 0,
			ThreadLocalStats = threadLocalStats,
		};
	}

	/// <summary>
	/// Releases all resources used by the <see cref="MessageEnvelopePool" />. Disposes the thread-local storage pool and cleans up all
	/// cached envelopes.
	/// </summary>
	public void Dispose() => _threadLocalPool?.Dispose();

	/// <summary>
	/// Returns an envelope's message to the pool.
	/// </summary>
	internal void Return<TMessage>(TMessage? message)
		where TMessage : class, IDispatchMessage
	{
		if (message == null || typeof(TMessage).IsValueType)
		{
			return;
		}

		_ = Interlocked.Increment(ref _totalReturns);

		// Return to message pool
		if (message is IPoolable poolable)
		{
			poolable.Reset();
		}

		// Return to the underlying message pool
		_messagePool.ReturnToPool(message);

		// Try to cache empty envelope in thread-local
		var localPool = _threadLocalPool.Value!;
		// R0.8: Dispose objects before losing scope - envelope is disposed in try block if not cached, or ownership transferred to cache on success, with disposal in catch block for exceptions
#pragma warning disable CA2000
		var emptyEnvelope = new MessageEnvelope<TMessage>(message, default!);
#pragma warning restore CA2000
		try
		{
			if (!localPool.TryReturn(emptyEnvelope))
			{
				// Envelope not cached, dispose it
				emptyEnvelope.Dispose();
			}
			else
			{
				// Ownership transferred to cache, set to default to prevent double dispose
				emptyEnvelope = default;
			}
		}
		catch
		{
			emptyEnvelope?.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Thread-local cache for envelopes.
	/// </summary>
	private sealed class LocalPool(int maxSize)
	{
		private readonly object[] _cache = new object[maxSize];
		private int _count;

		public bool TryRent<TMessage>(out MessageEnvelope<TMessage> envelope)
			where TMessage : IDispatchMessage
		{
			if (_count > 0)
			{
				for (var i = _count - 1; i >= 0; i--)
				{
					if (_cache[i] is MessageEnvelope<TMessage> cached)
					{
						envelope = cached;
						_cache[i] = _cache[--_count];
						_cache[_count] = null!;
						return true;
					}
				}
			}

			envelope = default!;
			return false;
		}

		public bool TryReturn<TMessage>(MessageEnvelope<TMessage> envelope)
			where TMessage : IDispatchMessage
		{
			if (_count < maxSize)
			{
				_cache[_count++] = envelope;
				return true;
			}

			return false;
		}

		public ThreadLocalPoolStats GetStats() => new() { CachedItems = _count, MaxSize = maxSize };
	}
}
