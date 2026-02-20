// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Orchestrates execution of <see cref="IDispatchMiddleware" /> components in their configured order.
/// </summary>
/// <param name="middlewares"> Collection of middleware to execute. </param>
/// <param name="applicabilityStrategy"> Optional strategy for determining middleware applicability. If null, uses default strategy. </param>
public sealed class DispatchPipeline(
	IEnumerable<IDispatchMiddleware> middlewares,
	IMiddlewareApplicabilityStrategy? applicabilityStrategy = null) : IDispatchPipeline
{
	/// <summary>
	/// Pre-sort and cache middleware to avoid repeated sorting.
	/// </summary>
	private readonly IDispatchMiddleware[] _ordered =
		[.. middlewares.OrderBy(static m => (int?)m.Stage ?? (int)DispatchMiddlewareStage.End)];

	/// <summary>
	/// Strategy for determining middleware applicability.
	/// </summary>
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy = applicabilityStrategy;

	/// <summary>
	/// Cache filtered middleware arrays per message type to avoid repeated filtering.
	/// </summary>
	private readonly ConcurrentDictionary<Type, IDispatchMiddleware[]> _filteredMiddlewareCache = new();

	/// <summary>
	/// Executes the middleware pipeline for the provided message.
	/// </summary>
	/// <param name="message"> Message being dispatched. </param>
	/// <param name="context"> Context associated with the message. </param>
	/// <param name="nextDelegate"> Delegate to invoke once the pipeline completes. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The final <see cref="IMessageResult" /> produced by the pipeline. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when required parameters are null. </exception>
	public ValueTask<IMessageResult> ExecuteAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Optimize for empty pipeline (common in tests)
		if (_ordered.Length == 0)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// Get filtered middleware for this message type (cached for performance)
		var applicableMiddleware = GetApplicableMiddleware(message.GetType());

		// Optimize for no applicable middleware
		if (applicableMiddleware.Length == 0)
		{
			return nextDelegate(message, context, cancellationToken);
		}

		// Use struct-based state machine to avoid closure allocations
		var state = new PipelineState
		{
			Middlewares = applicableMiddleware,
			Message = message,
			Context = context,
			FinalDelegate = nextDelegate,
			CancellationToken = cancellationToken,
			Index = 0,
		};

		return ExecuteNextAsync(state, cancellationToken);
	}

	/// <summary>
	/// Clears the internal middleware cache.
	/// </summary>
	/// <remarks>
	/// This method is primarily intended for testing scenarios where the cache needs to be reset. In production, the cache improves
	/// performance by avoiding repeated filtering operations.
	/// </remarks>
	public void ClearCache() => _filteredMiddlewareCache.Clear();

	/// <summary>
	/// Executes the next middleware in the pipeline using a state machine approach.
	/// </summary>
	[SuppressMessage("Style", "RCS1163:Unused parameter",
			Justification = "CancellationToken parameter required for signature compatibility with state machine pattern")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
			Justification = "CancellationToken parameter required for signature compatibility with state machine pattern")]
	private static async ValueTask<IMessageResult> ExecuteNextAsync(PipelineState state, CancellationToken cancellationToken)
	{
		// Check if we've reached the end of the pipeline
		if (state.Index >= state.Middlewares.Length)
		{
			return await state.FinalDelegate(state.Message, state.Context, state.CancellationToken).ConfigureAwait(false);
		}

		// Get current middleware and increment index for next call
		var currentMiddleware = state.Middlewares[state.Index];
		state.Index++;

		// Execute current middleware with continuation to next
		return await currentMiddleware.InvokeAsync(
			state.Message,
			state.Context,
			(msg, ctx, ct) =>
			{
				_ = msg;
				_ = ctx;
				return ExecuteNextAsync(state, ct);
			},
			state.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Filters middleware by applicability to avoid closure in hot path.
	/// </summary>
	private static IDispatchMiddleware[] FilterMiddlewareByApplicability(
		IDispatchMiddleware[] middlewares,
		IMiddlewareApplicabilityStrategy strategy,
		MessageKinds messageKinds)
	{
		var result = new List<IDispatchMiddleware>(middlewares.Length);
		foreach (var middleware in middlewares)
		{
			if (strategy.ShouldApplyMiddleware(middleware.ApplicableMessageKinds, messageKinds))
			{
				result.Add(middleware);
			}
		}

		return [.. result];
	}

	/// <summary>
	/// Gets the applicable middleware for a message type, using caching for performance.
	/// </summary>
	/// <param name="messageType"> The type of message being processed. </param>
	/// <returns> Array of applicable middleware in the correct order. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2067:Target method's parameter does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The source value does not have matching annotations.",
		Justification =
			"The message type is used as a dictionary key and for determining message kinds. No dynamic member access or reflection is performed on the type itself.")]
	private IDispatchMiddleware[] GetApplicableMiddleware(Type messageType) =>

		// Use cache to avoid repeated filtering per message type
		_filteredMiddlewareCache.GetOrAdd(
			messageType,
			static (key, self) =>
			{
				if (self._applicabilityStrategy is null)
				{
					return self._ordered;
				}

				var messageKinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(key);
				return FilterMiddlewareByApplicability(self._ordered, self._applicabilityStrategy, messageKinds);
			},
			this);

	/// <summary>
	/// State structure for pipeline execution to avoid closure allocations.
	/// </summary>
	private struct PipelineState
	{
		public IDispatchMiddleware[] Middlewares;
		public IDispatchMessage Message;
		public IMessageContext Context;
		public DispatchRequestDelegate FinalDelegate;
		public CancellationToken CancellationToken;
		public int Index;
	}
}
