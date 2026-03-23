// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Builds pre-compiled middleware chains at startup to eliminate per-dispatch closure allocations.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the chain-of-responsibility pattern using pre-compiled delegate chains.
/// The middleware chain is built once when first accessed (or at freeze time) and reused for all
/// subsequent dispatches. This approach eliminates 200-300 bytes of allocations per dispatch that
/// would otherwise occur from creating new closures on each invocation.
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
/// At dispatch time, the executor is invoked with the final handler. The executor iterates through
/// middleware without creating closures - middleware receive a delegate that calls back into the
/// executor's static method with explicit state passing.
/// </item>
/// <item>
/// The final handler (which varies per dispatch) is stored in the message context during
/// execution to avoid passing it through closures.
/// </item>
/// </list>
/// <para>
/// <strong>Trade-off:</strong> Handler state is written to context per invocation, which keeps
/// dispatch allocation-stable without per-dispatch closure creation.
/// </para>
/// </remarks>
internal sealed class MiddlewareChainBuilder
{
	private readonly record struct ChainCacheKey(Type MessageType, int PipelineSignature);

	private readonly IDispatchMiddleware[] _middlewares;
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;
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

#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _freezeLock = new();
#else
	private readonly object _freezeLock = new();
#endif
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
		_applicabilityStrategy = applicabilityStrategy;
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
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2067:Target method's parameter does not satisfy 'DynamicallyAccessedMembersAttribute'",
		Justification = "Message type is used only for dictionary key and message kind determination, not for reflection.")]
	private ChainExecutor CreateChainExecutor(Type messageType)
	{
		var applicableMiddleware = GetApplicableMiddleware(messageType);
		return new ChainExecutor(applicableMiddleware);
	}

	/// <summary>
	/// Creates a chain executor for a specific message type and explicit pipeline signature key.
	/// </summary>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2067:Target method's parameter does not satisfy 'DynamicallyAccessedMembersAttribute'",
		Justification = "Message type is used only for dictionary key and message kind determination, not for reflection.")]
	private ChainExecutor CreateChainExecutor(ChainCacheKey cacheKey)
	{
		return CreateChainExecutor(cacheKey.MessageType);
	}

	private static int ComputePipelineSignature(
		IReadOnlyList<IDispatchMiddleware> middlewares,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy)
	{
		var hash = new HashCode();
		hash.Add(middlewares.Count);

		foreach (var middleware in middlewares)
		{
			hash.Add(middleware.GetType().FullName, StringComparer.Ordinal);
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
		if (_applicabilityStrategy is null)
		{
			return _middlewares;
		}

		var messageKinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(messageType);
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
/// Executes a pre-compiled middleware chain without per-dispatch closure allocations.
/// </summary>
/// <remarks>
/// <para>
/// This executor is created once per message type and reused for all dispatches.
/// It pre-builds an array of <see cref="DispatchRequestDelegate"/> instances at construction time,
/// where each delegate is a static method that captures only its index and the middleware array.
/// </para>
/// <para>
/// The final handler (which varies per dispatch) is stored in <see cref="IMessageContext"/>
/// during execution. This allows the pre-built delegates to access it without closure allocation.
/// </para>
/// </remarks>
public sealed class ChainExecutor
{
	/// <summary>
	/// Empty executor for fast path when no middleware is applicable.
	/// </summary>
	public static readonly ChainExecutor Empty = new([]);

	/// <summary>
	/// The middleware in execution order.
	/// </summary>
	private readonly IDispatchMiddleware[] _middlewares;

	/// <summary>
	/// Pre-built delegates for each middleware position in the chain.
	/// _chainDelegates[i] invokes middleware[i] and passes _chainDelegates[i+1] as "next".
	/// _chainDelegates[_middlewares.Length] invokes the final handler from context state.
	/// </summary>
	private readonly DispatchRequestDelegate[] _chainDelegates;

	/// <summary>
	/// Context item key used to store the final handler when <see cref="IMessageContext"/>
	/// is not the framework <see cref="MessageContext"/>.
	/// </summary>
	private const string FinalHandlerContextKey = "Dispatch:Pipeline:FinalHandler";

	/// <summary>
	/// Context item key used to store typed final handlers when <see cref="IMessageContext"/>
	/// is not the framework <see cref="MessageContext"/>.
	/// </summary>
	private const string TypedFinalHandlerContextKey = "Dispatch:Pipeline:TypedFinalHandler";

	/// <summary>
	/// Initializes a new instance of the <see cref="ChainExecutor"/> class.
	/// </summary>
	/// <param name="middlewares">The middleware in execution order.</param>
	internal ChainExecutor(IDispatchMiddleware[] middlewares)
	{
		_middlewares = middlewares;

		// Build chain delegates: one for each middleware + one for the final handler
		_chainDelegates = new DispatchRequestDelegate[middlewares.Length + 1];

		// Terminal delegate: invokes the final handler from context state
		_chainDelegates[middlewares.Length] = InvokeFinalHandler;

		// Build backwards: each delegate invokes its middleware with the next delegate
		for (var i = middlewares.Length - 1; i >= 0; i--)
		{
			var middleware = middlewares[i];
			var nextDelegate = _chainDelegates[i + 1];

			// Capture only the middleware and nextDelegate - no per-dispatch state
			_chainDelegates[i] = (msg, ctx, ct) =>
				middleware.InvokeAsync(msg, ctx, nextDelegate, ct);
		}
	}

	/// <summary>
	/// Gets whether this chain has any middleware.
	/// </summary>
	public bool HasMiddleware => _middlewares.Length > 0;

	/// <summary>
	/// Gets whether this chain contains only routing middleware.
	/// </summary>
	/// <remarks>
	/// Dispatcher can pre-route local actions before middleware execution, so a routing-only chain
	/// can be bypassed for the direct-local fast path.
	/// </remarks>
	public bool HasOnlyRoutingMiddleware =>
		_middlewares.Length == 1 &&
		_middlewares[0] is RoutingMiddleware;

	/// <summary>
	/// Gets the number of middleware in this chain.
	/// </summary>
	public int Count => _middlewares.Length;

	/// <summary>
	/// Executes the middleware chain with the specified final handler.
	/// </summary>
	/// <param name="message">The message being dispatched.</param>
	/// <param name="context">The message context.</param>
	/// <param name="finalHandler">The final handler to invoke after all middleware.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the dispatch operation.</returns>
	/// <remarks>
	/// Returns <see cref="ValueTask{T}"/> to enable zero-allocation dispatch on synchronous completion paths.
	/// When no middleware applies, the final handler is invoked directly.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate finalHandler,
		CancellationToken cancellationToken)
	{
		// Fast path: no middleware - return directly without allocation
		if (_middlewares.Length == 0)
		{
			return finalHandler(message, context, cancellationToken);
		}

		// Slow path: execute middleware chain
		return InvokeChainAsync(message, context, finalHandler, cancellationToken);
	}

	/// <summary>
	/// Executes the middleware chain.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private ValueTask<IMessageResult> InvokeChainAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate finalHandler,
		CancellationToken cancellationToken)
	{
		object? previousHandler;
		bool hadPreviousHandler;

		if (context is MessageContext concreteContext)
		{
			hadPreviousHandler = concreteContext.TryGetPipelineFinalHandler(out previousHandler);
			concreteContext.SetPipelineFinalHandler(finalHandler);
		}
		else
		{
			hadPreviousHandler = context.Items.TryGetValue(FinalHandlerContextKey, out previousHandler);
			context.Items[FinalHandlerContextKey] = finalHandler;
		}

		try
		{
			var invocation = _chainDelegates[0](message, context, cancellationToken);
			if (invocation.IsCompletedSuccessfully)
			{
				try
				{
					return new ValueTask<IMessageResult>(invocation.Result);
				}
				finally
				{
					RestoreFinalHandler(context, previousHandler, hadPreviousHandler);
				}
			}

			return AwaitChainInvocation(invocation, context, previousHandler, hadPreviousHandler);
		}
		catch
		{
			RestoreFinalHandler(context, previousHandler, hadPreviousHandler);
			throw;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void RestoreFinalHandler(IMessageContext context, object? previousHandler, bool hadPreviousHandler)
	{
		if (context is MessageContext clearContext)
		{
			clearContext.SetPipelineFinalHandler(hadPreviousHandler ? previousHandler : null);
		}
		else
		{
			if (hadPreviousHandler)
			{
				context.Items[FinalHandlerContextKey] = previousHandler!;
			}
			else
			{
				context.Items.Remove(FinalHandlerContextKey);
			}
		}
	}

	private static async ValueTask<IMessageResult> AwaitChainInvocation(
		ValueTask<IMessageResult> invocation,
		IMessageContext context,
		object? previousHandler,
		bool hadPreviousHandler)
	{
		try
		{
			return await invocation.ConfigureAwait(false);
		}
		finally
		{
			RestoreFinalHandler(context, previousHandler, hadPreviousHandler);
		}
	}

	/// <summary>
	/// Executes the middleware chain with typed response.
	/// </summary>
	/// <remarks>
	/// Returns <see cref="ValueTask{T}"/> to enable zero-allocation dispatch on synchronous completion paths.
	/// When no middleware applies, the final handler is invoked directly.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public ValueTask<T> InvokeAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> finalHandler,
		CancellationToken cancellationToken)
		where T : IMessageResult
	{
		// Fast path: no middleware - return directly without allocation
		if (_middlewares.Length == 0)
		{
			return finalHandler(message, context, cancellationToken);
		}

		// Slow path: execute middleware chain
		return InvokeTypedChainAsync(message, context, finalHandler, cancellationToken);
	}

	/// <summary>
	/// Executes the typed middleware chain.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private ValueTask<T> InvokeTypedChainAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> finalHandler,
		CancellationToken cancellationToken)
		where T : IMessageResult
	{
		object? previousTypedHandler;
		bool hadPreviousTypedHandler;

		if (context is MessageContext concreteContext)
		{
			hadPreviousTypedHandler = concreteContext.TryGetPipelineTypedFinalHandler(out previousTypedHandler);
			concreteContext.SetPipelineTypedFinalHandler(finalHandler);
		}
		else
		{
			hadPreviousTypedHandler = context.Items.TryGetValue(TypedFinalHandlerContextKey, out previousTypedHandler);
			context.Items[TypedFinalHandlerContextKey] = finalHandler;
		}

		try
		{
			var invocation = InvokeAsync(message, context, TypedFinalHandlerInvoker<T>.Delegate, cancellationToken);
			if (invocation.IsCompletedSuccessfully)
			{
				try
				{
					return new ValueTask<T>((T)invocation.Result);
				}
				finally
				{
					RestoreTypedFinalHandler(context, previousTypedHandler, hadPreviousTypedHandler);
				}
			}

			return AwaitTypedInvocation<T>(invocation, context, previousTypedHandler, hadPreviousTypedHandler);
		}
		catch
		{
			RestoreTypedFinalHandler(context, previousTypedHandler, hadPreviousTypedHandler);
			throw;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void RestoreTypedFinalHandler(
		IMessageContext context,
		object? previousTypedHandler,
		bool hadPreviousTypedHandler)
	{
		if (context is MessageContext clearContext)
		{
			clearContext.SetPipelineTypedFinalHandler(hadPreviousTypedHandler ? previousTypedHandler : null);
		}
		else
		{
			if (hadPreviousTypedHandler)
			{
				context.Items[TypedFinalHandlerContextKey] = previousTypedHandler!;
			}
			else
			{
				context.Items.Remove(TypedFinalHandlerContextKey);
			}
		}
	}

	private static async ValueTask<T> AwaitTypedInvocation<T>(
		ValueTask<IMessageResult> invocation,
		IMessageContext context,
		object? previousTypedHandler,
		bool hadPreviousTypedHandler)
		where T : IMessageResult
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			return (T)result;
		}
		finally
		{
			RestoreTypedFinalHandler(context, previousTypedHandler, hadPreviousTypedHandler);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ValueTask<IMessageResult> AdaptTypedFinalHandlerResult<T>(ValueTask<T> typedResult)
		where T : IMessageResult
	{
		if (typedResult.IsCompletedSuccessfully)
		{
			return new ValueTask<IMessageResult>(typedResult.Result);
		}

		return AwaitTypedResult(typedResult);

		static async ValueTask<IMessageResult> AwaitTypedResult(ValueTask<T> pendingResult)
		{
			return await pendingResult.ConfigureAwait(false);
		}
	}

	private static class TypedFinalHandlerInvoker<T>
		where T : IMessageResult
	{
		public static readonly DispatchRequestDelegate Delegate = Invoke;

		private static ValueTask<IMessageResult> Invoke(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			var typedHandlerObject = (context is MessageContext concreteContext
				? (concreteContext.TryGetPipelineTypedFinalHandler(out var concreteHandler) ? concreteHandler : null)
				: (context.Items.TryGetValue(TypedFinalHandlerContextKey, out var ctxHandler) ? ctxHandler : null))
				?? throw new InvalidOperationException("Typed final handler not set in message context.");

			var typedHandler = (Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>>)typedHandlerObject;
			return AdaptTypedFinalHandlerResult(typedHandler(message, context, cancellationToken));
		}
	}

	/// <summary>
	/// Terminal delegate that invokes the final handler from context state.
	/// </summary>
	private static ValueTask<IMessageResult> InvokeFinalHandler(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		var handler = (context is MessageContext concreteContext &&
					  concreteContext.TryGetPipelineFinalHandler(out var concreteHandler)
					  ? (DispatchRequestDelegate)concreteHandler!
					  : (context.Items.TryGetValue(FinalHandlerContextKey, out var ctxHandler)
						  ? (DispatchRequestDelegate)ctxHandler!
						  : null))
					  ?? throw new InvalidOperationException("Final handler not set in message context.");

		return handler(message, context, cancellationToken);
	}
}
