// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.ZeroAlloc;

/// <summary>
/// Provides a pool of reusable message context instances.
/// </summary>
public sealed class MessageContextPool : IMessageContextPool
{
	private const int ThreadLocalCacheSize = 4;
	[ThreadStatic] private static PooledMessageContext?[]? s_threadLocalCache;
	[ThreadStatic] private static int s_threadLocalCacheCount;
	private readonly ObjectPool<PooledMessageContext> _pool;
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageContextPool" /> class.
	/// </summary>
	public MessageContextPool(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		var provider = new DefaultObjectPoolProvider();
		_pool = provider.Create(new MessageContextPooledObjectPolicy(_serviceProvider));
	}

	/// <inheritdoc />
	public IMessageContext Rent()
	{
		// Prefer thread-local reusable context to reduce shared pool contention.
		var cache = s_threadLocalCache;
		var count = s_threadLocalCacheCount;
		if (cache is { } cachedArray && count > 0)
		{
			var context = cachedArray[--count];
			cachedArray[count] = null;
			s_threadLocalCacheCount = count;

			// Thread-local cache is [ThreadStatic] (shared across all pool instances).
			// Re-initialize RequestServices to this pool's service provider to prevent
			// cross-pool contamination when multiple pools exist (e.g., in test suites).
			context.RequestServices = _serviceProvider;
			return context;
		}

		return _pool.Get();
	}

	/// <inheritdoc />
	public IMessageContext Rent(IDispatchMessage message)
	{
		var context = (PooledMessageContext)Rent();
		context.Initialize(message);
		return context;
	}

	/// <inheritdoc />
	public void ReturnToPool(IMessageContext context)
	{
		if (context is PooledMessageContext pooled)
		{
			var cache = s_threadLocalCache;
			var count = s_threadLocalCacheCount;
			if (cache is null)
			{
				cache = s_threadLocalCache = new PooledMessageContext?[ThreadLocalCacheSize];
				count = 0;
			}

			if (count < ThreadLocalCacheSize)
			{
				pooled.Reset();
				cache[count] = pooled;
				s_threadLocalCacheCount = count + 1;
				return;
			}

			_pool.Return(pooled);
		}
	}

	private sealed class MessageContextPooledObjectPolicy(IServiceProvider serviceProvider) : PooledObjectPolicy<PooledMessageContext>
	{
		public override PooledMessageContext Create() => new(serviceProvider);

		public override bool Return(PooledMessageContext obj)
		{
			obj.Reset();
			return true;
		}
	}

	private sealed class PooledMessageContext(IServiceProvider serviceProvider)
		: MessageContext(EmptyMessage.Instance, serviceProvider)
	{
		public void Initialize(IDispatchMessage message) => Message = message;

		public new void Reset()
		{
			base.Reset();
			Message = null;
			Result = null;
		}
	}
}
