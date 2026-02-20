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
/// The final handler (which varies per dispatch) is stored in an <see cref="AsyncLocal{T}"/> during
/// execution to avoid passing it through closures.
/// </item>
/// </list>
/// <para>
/// <strong>Trade-off:</strong> Using AsyncLocal adds a small constant overhead but eliminates the
/// per-middleware-per-dispatch closure allocation, which is a net win for pipelines with 2+ middleware.
/// </para>
/// </remarks>
public sealed class MiddlewareChainBuilder
{
	private readonly IDispatchMiddleware[] _middlewares;
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;

	/// <summary>
	/// Cache of pre-compiled chain executors per message type.
	/// </summary>
	private readonly ConcurrentDictionary<Type, ChainExecutor> _chainCache = new();

	/// <summary>
	/// Frozen cache for optimal read performance after freeze.
	/// </summary>
	private FrozenDictionary<Type, ChainExecutor>? _frozenChainCache;

#if NET9_0_OR_GREATER
	private readonly Lock _freezeLock = new();
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
		_middlewares = middlewares?
			.OrderBy(static m => m.Stage ?? DispatchMiddlewareStage.End)
			.ToArray() ?? [];
		_applicabilityStrategy = applicabilityStrategy;
	}

	/// <summary>
	/// Gets whether the builder has been frozen.
	/// </summary>
	public bool IsFrozen => _isFrozen;

	/// <summary>
	/// Gets the pre-compiled chain executor for a specific message type.
	/// </summary>
	/// <param name="messageType">The type of message being dispatched.</param>
	/// <returns>A chain executor that can execute middleware without per-dispatch closures.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ChainExecutor GetChain(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		// Fast path: frozen cache lookup
		if (_frozenChainCache is not null)
		{
			return _frozenChainCache.TryGetValue(messageType, out var cached)
				? cached
				: CreateChainExecutor(messageType);
		}

		// Concurrent cache lookup/creation
		return _chainCache.GetOrAdd(messageType, CreateChainExecutor);
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
					_ = _chainCache.GetOrAdd(type, CreateChainExecutor);
				}
			}

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
/// The final handler (which varies per dispatch) is stored in <see cref="AsyncLocal{T}"/> during
/// execution. This allows the pre-built delegates to access it without closure allocation.
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
	/// _chainDelegates[_middlewares.Length] invokes the final handler from AsyncLocal.
	/// </summary>
	private readonly DispatchRequestDelegate[] _chainDelegates;

	/// <summary>
	/// AsyncLocal storage for the final handler during execution.
	/// This avoids passing the handler through closures.
	/// </summary>
	private static readonly AsyncLocal<DispatchRequestDelegate?> _currentFinalHandler = new();

	/// <summary>
	/// Context item key used to store the final handler as a fallback when AsyncLocal
	/// doesn't flow (e.g., HybridCache's BackgroundFetchAsync runs on a separate context).
	/// </summary>
	private const string FinalHandlerContextKey = "Dispatch:Pipeline:FinalHandler";

	/// <summary>
	/// Initializes a new instance of the <see cref="ChainExecutor"/> class.
	/// </summary>
	/// <param name="middlewares">The middleware in execution order.</param>
	internal ChainExecutor(IDispatchMiddleware[] middlewares)
	{
		_middlewares = middlewares;

		// Build chain delegates: one for each middleware + one for the final handler
		_chainDelegates = new DispatchRequestDelegate[middlewares.Length + 1];

		// Terminal delegate: invokes the final handler from AsyncLocal
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
	/// Executes the middleware chain (slow path with AsyncLocal state management).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private async ValueTask<IMessageResult> InvokeChainAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate finalHandler,
		CancellationToken cancellationToken)
	{
		// Store final handler in AsyncLocal for access by terminal delegate
		var previousHandler = _currentFinalHandler.Value;
		_currentFinalHandler.Value = finalHandler;

		// Store fallback without forcing MessageContext.Items allocation.
		if (context is MessageContext concreteContext)
		{
			concreteContext.SetPipelineFinalHandler(finalHandler);
		}
		else
		{
			context.Items[FinalHandlerContextKey] = finalHandler;
		}

		try
		{
			// Start the pre-built chain
			return await _chainDelegates[0](message, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Restore previous handler (for nested dispatches)
			_currentFinalHandler.Value = previousHandler;
			if (context is MessageContext clearContext)
			{
				clearContext.SetPipelineFinalHandler(handler: null);
			}
			else
			{
				context.Items.Remove(FinalHandlerContextKey);
			}
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
	/// Executes the typed middleware chain (slow path with wrapper allocation).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private async ValueTask<T> InvokeTypedChainAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> finalHandler,
		CancellationToken cancellationToken)
		where T : IMessageResult
	{
		// Wrap the typed handler - this is one allocation per dispatch for typed handlers
		// but much smaller than the N closures we were creating before
		var wrappedHandler = new DispatchRequestDelegate(async (msg, ctx, ct) => await finalHandler(msg, ctx, ct).ConfigureAwait(false));

		var result = await InvokeAsync(message, context, wrappedHandler, cancellationToken).ConfigureAwait(false);
		return (T)result;
	}

	/// <summary>
	/// Terminal delegate that invokes the final handler from AsyncLocal,
	/// falling back to the IMessageContext if AsyncLocal doesn't flow
	/// (e.g., when HybridCache executes the factory on a background thread).
	/// </summary>
	private static ValueTask<IMessageResult> InvokeFinalHandler(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Fallback to context: AsyncLocal doesn't flow when HybridCache runs factory on a background thread
		var handler = _currentFinalHandler.Value
					  ?? (context is MessageContext concreteContext &&
						  concreteContext.TryGetPipelineFinalHandler(out var concreteHandler)
						  ? (DispatchRequestDelegate)concreteHandler!
						  : (context.Items.TryGetValue(FinalHandlerContextKey, out var ctxHandler)
							  ? (DispatchRequestDelegate)ctxHandler!
							  : null))
					  ?? throw new InvalidOperationException("Final handler not set in AsyncLocal context.");

		return handler(message, context, cancellationToken);
	}
}
