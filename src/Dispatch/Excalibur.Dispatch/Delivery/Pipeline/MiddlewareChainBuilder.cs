// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Builds pre-compiled middleware chains at startup to eliminate per-dispatch closure allocations.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the chain-of-responsibility pattern using pre-compiled delegate chains.
/// Middleware selection is cached per message type. Each executor lazily binds its first terminal
/// delegate and reuses that chain on subsequent dispatches with the same terminal identity.
/// </para>
/// <para>
/// <strong>How it works:</strong>
/// </para>
/// <list type="number">
/// <item>
/// At build time, we create a <see cref="ChainExecutor"/> that holds references to all applicable
/// middleware for a message type.
/// </item>
/// <item>
/// Middleware receives an immutable continuation that forwards the supplied message, context and
/// cancellation token. No invocation state is written onto the message context.
/// </item>
/// <item>
/// Each executor binds its terminal delegate into an immutable chain, caching the first terminal
/// identity and using an uncached chain for other identities.
/// </item>
/// </list>
/// <para>
/// <strong>Trade-off:</strong> Each executor weakly caches its first terminal identity. Stable
/// terminal delegates reuse the chain; changing terminal delegates allocate a new chain.
/// </para>
/// </remarks>
internal sealed class MiddlewareChainBuilder
{
	private readonly record struct ChainCacheKey(Type MessageType, int PipelineSignature);

	private readonly IDispatchMiddleware[] _middlewares;
	private readonly IMiddlewareApplicabilityStrategy _applicabilityStrategy;
	private readonly ConcurrentDictionary<MessageKinds, ChainExecutor> _customChains = new();
	private readonly int _pipelineSignature;

	/// <summary>
	/// Fast-path cache of pre-compiled chain executors keyed by message type for this builder's pipeline signature.
	/// </summary>
	private readonly ConcurrentDictionary<Type, ChainExecutor> _chainCacheByType = new();

	/// <summary>
	/// Optional cache of pre-compiled chain executors keyed by message type and explicit pipeline signature.
	/// This is only used for non-default signature lookups.
	/// </summary>
	private readonly ConcurrentDictionary<ChainCacheKey, ChainExecutor> _chainCache = new();

	/// <summary>
	/// Frozen fast-path cache for optimal read performance after freeze.
	/// </summary>
	private FrozenDictionary<Type, ChainExecutor>? _frozenChainCacheByType;

	/// <summary>
	/// Frozen fallback cache for explicit non-default signature lookups after freeze.
	/// </summary>
	private FrozenDictionary<ChainCacheKey, ChainExecutor>? _frozenChainCache;

	private readonly Lock _freezeLock = new();
	private volatile bool _isFrozen;

	/// <summary>
	/// Initializes a new instance of the <see cref="MiddlewareChainBuilder"/> class.
	/// </summary>
	/// <param name="middlewares">The middleware components to include in chains.</param>
	/// <param name="applicabilityStrategy">Optional strategy for determining middleware applicability.</param>
	public MiddlewareChainBuilder(
		IEnumerable<IDispatchMiddleware> middlewares,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy = null)
	{
		// Sort middleware by Stage to ensure correct pipeline ordering regardless of DI registration order.
		// Middleware with null Stage defaults to End (1000) to run last.
		_middlewares = SortMiddlewaresByStage(middlewares);
		_applicabilityStrategy = applicabilityStrategy ?? new DefaultMiddlewareApplicabilityStrategy();
		_pipelineSignature = ComputePipelineSignature(_middlewares, _applicabilityStrategy);
	}

	private static IDispatchMiddleware[] SortMiddlewaresByStage(IEnumerable<IDispatchMiddleware>? middlewares)
	{
		if (middlewares is null)
		{
			return [];
		}

		if (middlewares is IReadOnlyList<IDispatchMiddleware> list)
		{
			var count = list.Count;
			if (count == 0)
			{
				return [];
			}

			var sortedArray = new IDispatchMiddleware[count];
			for (var i = 0; i < count; i++)
			{
				sortedArray[i] = list[i];
			}

			SortByStageInPlace(sortedArray);

			return sortedArray;
		}

		var sortedList = middlewares switch
		{
			ICollection<IDispatchMiddleware> collection => new List<IDispatchMiddleware>(collection.Count),
			_ => new List<IDispatchMiddleware>()
		};

		foreach (var middleware in middlewares)
		{
			sortedList.Add(middleware);
		}

		if (sortedList.Count == 0)
		{
			return [];
		}

		var reorderedArray = new IDispatchMiddleware[sortedList.Count];
		sortedList.CopyTo(reorderedArray, 0);
		SortByStageInPlace(reorderedArray);
		return reorderedArray;
	}

	private static void SortByStageInPlace(IDispatchMiddleware[] middlewares)
	{
		for (var i = 1; i < middlewares.Length; i++)
		{
			var current = middlewares[i];
			var currentStage = current.Stage ?? DispatchMiddlewareStage.End;
			var j = i - 1;

			while (j >= 0 && (middlewares[j].Stage ?? DispatchMiddlewareStage.End) > currentStage)
			{
				middlewares[j + 1] = middlewares[j];
				j--;
			}

			middlewares[j + 1] = current;
		}
	}

	/// <summary>
	/// Gets whether the builder has been frozen.
	/// </summary>
	public bool IsFrozen => _isFrozen;

	/// <summary>
	/// Gets the middleware pipeline signature used for chain-cache partitioning.
	/// </summary>
	internal int PipelineSignature => _pipelineSignature;

	internal bool HasCustomApplicability => _applicabilityStrategy is not DefaultMiddlewareApplicabilityStrategy;

	internal ChainExecutor GetChain(IDispatchMessage message, int pipelineSignature) =>
		HasCustomApplicability
			? _customChains.GetOrAdd(_applicabilityStrategy.DetermineMessageKinds(message),
				static (kinds, self) => new ChainExecutor(self.FilterMiddleware(kinds)), this)
			: GetChain(message.GetType(), pipelineSignature);

	/// <summary>
	/// Gets the pre-compiled chain executor for a specific message type.
	/// </summary>
	/// <param name="messageType">The type of message being dispatched.</param>
	/// <returns>A chain executor that can execute middleware without per-dispatch closures.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ChainExecutor GetChain(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		// Fast path: frozen cache lookup keyed only by message type.
		if (_frozenChainCacheByType is not null)
		{
			return _frozenChainCacheByType.TryGetValue(messageType, out var cached)
				? cached
				: _chainCacheByType.GetOrAdd(messageType, CreateChainExecutor);
		}

		// Concurrent cache lookup/creation keyed by message type.
		return _chainCacheByType.GetOrAdd(messageType, CreateChainExecutor);
	}

	/// <summary>
	/// Gets the pre-compiled chain executor for a specific message type and pipeline signature.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ChainExecutor GetChain(Type messageType, int pipelineSignature)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		// Default signature dominates hot path; use type-keyed fast cache.
		if (pipelineSignature == _pipelineSignature)
		{
			return GetChain(messageType);
		}

		var cacheKey = new ChainCacheKey(messageType, pipelineSignature);

		// Fast path: frozen fallback cache lookup for non-default signature path.
		if (_frozenChainCache is not null)
		{
			return _frozenChainCache.TryGetValue(cacheKey, out var cached)
				? cached
				: _chainCache.GetOrAdd(cacheKey, CreateChainExecutor);
		}

		// Concurrent fallback cache lookup/creation for non-default signature path.
		return _chainCache.GetOrAdd(cacheKey, CreateChainExecutor);
	}

	/// <summary>
	/// Freezes the chain cache for optimal read performance.
	/// Should be called after all message types have been registered.
	/// </summary>
	/// <param name="knownMessageTypes">Known message types to pre-compile chains for.</param>
	public void Freeze(IEnumerable<Type>? knownMessageTypes = null)
	{
		if (_isFrozen)
		{
			return;
		}

		lock (_freezeLock)
		{
			if (_isFrozen)
			{
				return;
			}

			// Pre-compile chains for known types
			if (knownMessageTypes is not null)
			{
				foreach (var type in knownMessageTypes)
				{
					_ = _chainCacheByType.GetOrAdd(type, CreateChainExecutor);
				}
			}

			_frozenChainCacheByType = _chainCacheByType.ToFrozenDictionary();
			_frozenChainCache = _chainCache.ToFrozenDictionary();
			_isFrozen = true;
		}
	}

	/// <summary>
	/// Creates a chain executor for a specific message type.
	/// </summary>
	private ChainExecutor CreateChainExecutor(Type messageType)
	{
		var applicableMiddleware = GetApplicableMiddleware(messageType);
		return new ChainExecutor(applicableMiddleware);
	}

	/// <summary>
	/// Creates a chain executor for a specific message type and explicit pipeline signature key.
	/// </summary>
	private ChainExecutor CreateChainExecutor(ChainCacheKey cacheKey)
	{
		return CreateChainExecutor(cacheKey.MessageType);
	}

	private static int ComputePipelineSignature(
		IDispatchMiddleware[] middlewares,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy)
	{
		var hash = new HashCode();
		hash.Add(middlewares.Length);

		foreach (var middleware in middlewares)
		{
			hash.Add(MiddlewareIdentity.TypeOf(middleware).FullName, StringComparer.Ordinal);
			hash.Add((int)(middleware.Stage ?? DispatchMiddlewareStage.End));
			hash.Add((int)middleware.ApplicableMessageKinds);
		}

		hash.Add(applicabilityStrategy?.GetType().FullName, StringComparer.Ordinal);
		return hash.ToHashCode();
	}

	/// <summary>
	/// Gets the applicable middleware for a message type.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2067:Target method's parameter does not satisfy 'DynamicallyAccessedMembersAttribute'",
		Justification = "Message type is used only for determining message kinds, not for dynamic member access or reflection.")]
	private IDispatchMiddleware[] GetApplicableMiddleware(Type messageType)
	{
		if (_middlewares.All(static middleware => middleware.ApplicableMessageKinds == MessageKinds.All))
		{
			return _middlewares;
		}

		var messageKinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(messageType);
		return FilterMiddleware(messageKinds);
	}

	private IDispatchMiddleware[] FilterMiddleware(MessageKinds messageKinds)
	{
		var result = new List<IDispatchMiddleware>(_middlewares.Length);

		foreach (var middleware in _middlewares)
		{
			if (_applicabilityStrategy.ShouldApplyMiddleware(middleware.ApplicableMessageKinds, messageKinds))
			{
				result.Add(middleware);
			}
		}

		return [.. result];
	}
}

/// <summary>
/// Executes immutable middleware chains bound to their terminal delegates.
/// </summary>
/// <remarks>
/// <para>
/// This executor is created once per message type and reused for all dispatches.
/// It snapshots the middleware at construction and caches the first terminal-bound chain for each
/// overload family. Reusing that delegate identity avoids per-dispatch closure allocations.
/// </para>
/// <para>
/// Terminal delegates are bound into immutable chains. Continuations remain valid when middleware
/// replaces a context or continues after returning; execution state is not stored on the context.
/// </para>
/// </remarks>
public sealed class ChainExecutor
{
	/// <summary>Empty executor for the direct-handler path.</summary>
	public static readonly ChainExecutor Empty = new([]);

	private readonly IDispatchMiddleware[] _middlewares;
	private ConditionalWeakTable<Delegate, BoundChain>? _boundChain;
	private ConditionalWeakTable<Delegate, BoundChain>? _typedBoundChain;

	/// <summary>Initializes an immutable middleware snapshot.</summary>
	internal ChainExecutor(IDispatchMiddleware[] middlewares) => _middlewares = [.. middlewares];

	/// <summary>Gets whether any middleware applies.</summary>
	public bool HasMiddleware => _middlewares.Length > 0;

	/// <summary>Gets whether only routing middleware applies.</summary>
	public bool HasOnlyRoutingMiddleware =>
		_middlewares.Length == 1 && MiddlewareIdentity.IsRouting(_middlewares[0]);

	/// <summary>Gets the number of middleware components.</summary>
	public int Count => _middlewares.Length;

	/// <summary>Executes the chain with the supplied terminal delegate.</summary>
	/// <remarks>
	/// The first terminal is weakly keyed so captured request objects are not retained by this executor.
	/// Repeated calls with that delegate reuse an immutable chain without per-dispatch closures.
	/// Different terminals build independent uncached chains; the cache never adds a second identity.
	/// </remarks>
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate finalHandler,
		CancellationToken cancellationToken)
	{
		if (_middlewares.Length == 0)
		{
			return finalHandler(message, context, cancellationToken);
		}

		var cache = Volatile.Read(ref _boundChain);
		if (cache is null || !cache.TryGetValue(finalHandler, out var chain))
		{
			var created = new BoundChain(Bind(finalHandler));
			if (cache is null)
			{
				var candidate = new ConditionalWeakTable<Delegate, BoundChain>();
				candidate.Add(finalHandler, created);
				_ = Interlocked.CompareExchange(ref _boundChain, candidate, null);
			}
			// A concurrent caller may have cached a DIFFERENT terminal. Always execute ours.
			return created.Entry(message, context, cancellationToken);
		}

		return chain.Entry(message, context, cancellationToken);
	}

	/// <summary>Executes the chain with a typed terminal delegate.</summary>
	/// <remarks>
	/// One typed terminal is cached independently of the untyped terminal. Middleware results must
	/// satisfy <typeparamref name="T"/>; callers requiring adaptation should invoke with IMessageResult.
	/// </remarks>
	public ValueTask<T> InvokeAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> finalHandler,
		CancellationToken cancellationToken)
		where T : IMessageResult
	{
		if (_middlewares.Length == 0)
		{
			return finalHandler(message, context, cancellationToken);
		}

		var cache = Volatile.Read(ref _typedBoundChain);
		BoundChain? chain = null;
		if (cache is null || !cache.TryGetValue(finalHandler, out chain))
		{
			var created = BindTyped(finalHandler);
			if (cache is null)
			{
				var candidate = new ConditionalWeakTable<Delegate, BoundChain>();
				candidate.Add(finalHandler, created);
				_ = Interlocked.CompareExchange(ref _typedBoundChain, candidate, null);
			}
			chain = created;
		}

		var invocation = chain.Entry(message, context, cancellationToken);
		return invocation.IsCompletedSuccessfully
			? new ValueTask<T>((T)invocation.Result)
			: AwaitInvocation<T>(invocation);
	}

	private BoundChain BindTyped<T>(Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> terminal)
		where T : IMessageResult =>
		new(Bind((message, context, token) => AdaptTypedResult(terminal(message, context, token))));

	private DispatchRequestDelegate Bind(DispatchRequestDelegate terminal)
	{
		var next = terminal;
		for (var index = _middlewares.Length - 1; index >= 0; index--)
		{
			var middleware = _middlewares[index];
			var continuation = next;
			next = (message, context, token) => middleware.InvokeAsync(message, context, continuation, token);
		}
		return next;
	}

	private static async ValueTask<T> AwaitInvocation<T>(ValueTask<IMessageResult> invocation)
		where T : IMessageResult => (T)await invocation.ConfigureAwait(false);

	private static ValueTask<IMessageResult> AdaptTypedResult<T>(ValueTask<T> result)
		where T : IMessageResult => result.IsCompletedSuccessfully
			? new ValueTask<IMessageResult>(result.Result)
			: AwaitTypedResult(result);

	private static async ValueTask<IMessageResult> AwaitTypedResult<T>(ValueTask<T> result)
		where T : IMessageResult => await result.ConfigureAwait(false);

	private sealed class BoundChain(DispatchRequestDelegate entry)
	{
		public DispatchRequestDelegate Entry { get; } = entry;
	}
}
