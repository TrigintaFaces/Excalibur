// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Configurable message context pool that wraps <see cref="ObjectPool{T}"/>
/// with bounded size, pre-warming, and optional metrics tracking.
/// </summary>
/// <remarks>
/// <para>
/// This pool manages <see cref="IMessageContext"/> instances to reduce GC pressure
/// in high-throughput dispatch scenarios. It uses <see cref="Microsoft.Extensions.ObjectPool"/>
/// as the underlying pool implementation.
/// </para>
/// <para>
/// Enable via <c>services.AddContextPooling(options => { options.Enabled = true; })</c>.
/// </para>
/// </remarks>
public sealed class MessageContextPoolAdapter : IMessageContextPool
{
	private readonly ObjectPool<PoolableMessageContext> _pool;
	private readonly IServiceProvider _serviceProvider;
	private readonly ContextPoolingOptions _options;
	private long _rentCount;
	private long _returnCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageContextPoolAdapter"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for creating new contexts.</param>
	/// <param name="options">The pooling configuration options.</param>
	public MessageContextPoolAdapter(
		IServiceProvider serviceProvider,
		IOptions<ContextPoolingOptions> options)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));

		var provider = new DefaultObjectPoolProvider { MaximumRetained = _options.MaxPoolSize };
		_pool = provider.Create(new ContextPoolPolicy(_serviceProvider));

		// Pre-warm the pool
		if (_options.PreWarmCount > 0)
		{
			PreWarm(_options.PreWarmCount);
		}
	}

	/// <summary>
	/// Gets the number of contexts rented from the pool.
	/// </summary>
	/// <value>The total rent count since pool creation.</value>
	public long RentCount => Interlocked.Read(ref _rentCount);

	/// <summary>
	/// Gets the number of contexts returned to the pool.
	/// </summary>
	/// <value>The total return count since pool creation.</value>
	public long ReturnCount => Interlocked.Read(ref _returnCount);

	/// <inheritdoc />
	public IMessageContext Rent()
	{
		if (_options.TrackMetrics)
		{
			Interlocked.Increment(ref _rentCount);
		}

		return _pool.Get();
	}

	/// <inheritdoc />
	public IMessageContext Rent(IDispatchMessage message)
	{
		if (_options.TrackMetrics)
		{
			Interlocked.Increment(ref _rentCount);
		}

		var context = _pool.Get();
		context.Initialize(message);
		return context;
	}

	/// <inheritdoc />
	public void ReturnToPool(IMessageContext context)
	{
		if (context is PoolableMessageContext poolable)
		{
			if (_options.TrackMetrics)
			{
				Interlocked.Increment(ref _returnCount);
			}

			poolable.Reset();
			_pool.Return(poolable);
		}
	}

	/// <summary>
	/// Pre-warms the pool by creating and immediately returning contexts.
	/// </summary>
	private void PreWarm(int count)
	{
		var contexts = new PoolableMessageContext[count];
		for (var i = 0; i < count; i++)
		{
			contexts[i] = _pool.Get();
		}

		for (var i = 0; i < count; i++)
		{
			_pool.Return(contexts[i]);
		}
	}

	private sealed class ContextPoolPolicy(IServiceProvider serviceProvider)
		: PooledObjectPolicy<PoolableMessageContext>
	{
		public override PoolableMessageContext Create() => new(serviceProvider);

		public override bool Return(PoolableMessageContext obj)
		{
			obj.Reset();
			return true;
		}
	}
}

/// <summary>
/// A message context implementation that supports object pooling with efficient reset.
/// </summary>
internal sealed class PoolableMessageContext(IServiceProvider serviceProvider)
	: MessageContext(EmptyMessage.Instance, serviceProvider)
{
	/// <summary>
	/// Initializes the pooled context with the specified message.
	/// </summary>
	/// <param name="message">The message to associate with this context.</param>
	public void Initialize(IDispatchMessage message) => Message = message;

	/// <summary>
	/// Resets the context to its initial state for pooling reuse.
	/// </summary>
	public new void Reset()
	{
		Message = null;
		Result = null;
		CorrelationId = null;
		CausationId = null;
		TenantId = null;
		SessionId = null;
		WorkflowId = null;
		PartitionKey = null;
		ExternalId = null;
		UserId = null;
		Source = null;
		MessageType = null;
		ContentType = null;
		TraceParent = null;
		DeliveryCount = 0;
		ProcessingAttempts = 0;
		IsRetry = false;
		ValidationPassed = false;
		ValidationTimestamp = null;
		Transaction = null;
		TransactionId = null;
		TimeoutExceeded = false;
		TimeoutElapsed = null;
		RateLimitExceeded = false;
		RateLimitRetryAfter = null;
		FirstAttemptTime = null;
		RoutingDecision = null;
		Items.Clear();
	}
}
