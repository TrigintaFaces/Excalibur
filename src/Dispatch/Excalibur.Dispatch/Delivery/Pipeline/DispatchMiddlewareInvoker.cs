// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Executes a collection of <see cref="IDispatchMiddleware" /> instances in sequence,
/// filtering by <see cref="IDispatchMiddleware.ApplicableMessageKinds" />.
/// </summary>
/// <remarks>
/// <para>
/// This invoker uses pre-compiled middleware chains to eliminate closure allocations in the hot path.
/// The <see cref="MiddlewareChainBuilder"/> pre-builds delegate chains at startup, and this invoker
/// simply retrieves and executes the appropriate chain for each message type.
/// </para>
/// <para>
/// <strong>Performance characteristics:</strong>
/// </para>
/// <list type="bullet">
/// <item>Zero closure allocations per dispatch (chains are pre-built)</item>
/// <item>FrozenDictionary lookup for O(1) chain retrieval after freeze</item>
/// <item>Fast path bypasses chain execution when no middleware applies</item>
/// </list>
/// </remarks>
internal sealed class DispatchMiddlewareInvoker : IDispatchMiddlewareInvoker
{
	private readonly MiddlewareChainBuilder _chainBuilder;
	private readonly int _pipelineSignature;
	private readonly int _middlewareCount;
	private volatile bool _autoFrozen;

	// PERF-T3: Global bypass flag computed at construction time. When all registered middleware
	// is routing-only (or none applies), CanBypassFor() can return true instantly without
	// the FrozenDictionary chain lookup (~1-3μs saved per dispatch).
	private readonly bool _hasAnyNonRoutingMiddleware;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchMiddlewareInvoker"/> class.
	/// </summary>
	/// <param name="middlewares">The middleware components to invoke.</param>
	/// <param name="applicabilityStrategy">Optional strategy for determining middleware applicability.</param>
	public DispatchMiddlewareInvoker(
		IEnumerable<IDispatchMiddleware> middlewares,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy = null)
	{
		var middlewareArray = middlewares?.ToArray() ?? [];
		_chainBuilder = new MiddlewareChainBuilder(middlewareArray, applicabilityStrategy);
		_pipelineSignature = _chainBuilder.PipelineSignature;
		_middlewareCount = middlewareArray.Length;

		// PERF-T3: Pre-compute whether any non-routing middleware exists. If all middleware
		// is routing-only, CanBypassFor() can skip the chain lookup entirely.
		_hasAnyNonRoutingMiddleware = middlewareArray.Any(m => m is not RoutingMiddleware);
	}

	/// <summary>
	/// Gets a value indicating whether any middleware is configured.
	/// </summary>
	internal bool HasMiddleware => _middlewareCount > 0;

	/// <summary>
	/// Determines whether the middleware chain can be bypassed for the specified message type.
	/// </summary>
	/// <remarks>
	/// Returns <c>true</c> when no middleware is registered or when the compiled chain for
	/// the message type contains no applicable middleware components.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool CanBypassFor(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		if (_middlewareCount == 0)
		{
			return true;
		}

		// PERF-T3: Global fast-exit when all middleware is routing-only.
		// A single volatile bool read (~1ns) avoids the FrozenDictionary chain lookup (~3-5μs).
		if (!_hasAnyNonRoutingMiddleware)
		{
			return true;
		}

		if (!_autoFrozen && !_chainBuilder.IsFrozen)
		{
			_chainBuilder.Freeze();
			_autoFrozen = true;
		}

		var chain = _chainBuilder.GetChain(messageType, _pipelineSignature);
		return !chain.HasMiddleware || chain.HasOnlyRoutingMiddleware;
	}

	/// <summary>
	/// Executes the middleware chain and returns the final <see cref="IMessageResult" />.
	/// </summary>
	/// <typeparam name="T">Expected result type.</typeparam>
	/// <param name="message">Message being dispatched.</param>
	/// <param name="context">Context associated with the message.</param>
	/// <param name="nextDelegate">Delegate to invoke after middleware execution.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>The result returned by the final middleware or handler.</returns>
	/// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
	/// <remarks>
	/// Returns <see cref="ValueTask{T}"/> to avoid Task allocation on synchronous completion paths.
	/// When no middleware applies or middleware count is zero, the delegate is invoked directly
	/// without allocating a Task wrapper.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public ValueTask<T> InvokeAsync<T>(
		IDispatchMessage message,
		IMessageContext context,
		Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<T>> nextDelegate,
		CancellationToken cancellationToken)
		where T : IMessageResult
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Fast path for no middleware - returns ValueTask directly (no Task allocation)
		if (_middlewareCount == 0)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// PERF-T3: Fast path when all middleware is routing-only — skip chain lookup entirely.
		if (!_hasAnyNonRoutingMiddleware)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// Auto-freeze on first dispatch to prevent per-dispatch ConcurrentDictionary overhead.
		// Uses volatile read + Freeze()'s internal double-check lock for thread safety.
		if (!_autoFrozen && !_chainBuilder.IsFrozen)
		{
			_chainBuilder.Freeze();
			_autoFrozen = true;
		}

		// Get pre-compiled chain for this message type
		var chain = _chainBuilder.GetChain(message.GetType(), _pipelineSignature);

		// Fast path for no applicable middleware - returns ValueTask directly (no Task allocation)
		if (!chain.HasMiddleware)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// Execute the pre-compiled chain (may allocate Task if async path is taken)
		return chain.InvokeAsync(message, context, nextDelegate, cancellationToken);
	}

	/// <summary>
	/// Freezes the middleware chain cache for optimal read performance.
	/// </summary>
	/// <remarks>
	/// This method should be called during application startup after all message types have been
	/// registered. After freezing, the chain cache uses <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}"/>
	/// for optimal read performance.
	/// </remarks>
	/// <param name="knownMessageTypes">Optional collection of known message types to pre-compile chains for.</param>
	public void Freeze(IEnumerable<Type>? knownMessageTypes = null)
	{
		_chainBuilder.Freeze(knownMessageTypes);
	}

	/// <summary>
	/// Gets whether the invoker has been frozen.
	/// </summary>
	public bool IsFrozen => _chainBuilder.IsFrozen;
}
