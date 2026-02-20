// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Executes middleware with applicability filtering based on message kinds and enabled features. Implements requirements R2.4-R2.6.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="FilteredDispatchMiddlewareInvoker" /> class. </remarks>
/// <param name="middleware"> The collection of all registered middleware. </param>
/// <param name="applicabilityEvaluator"> The applicability evaluator. </param>
/// <param name="enabledFeatures"> The set of enabled features. </param>
/// <param name="options"> Configuration options. </param>
/// <param name="logger"> The logger. </param>
public sealed partial class FilteredDispatchMiddlewareInvoker(
	IEnumerable<IDispatchMiddleware> middleware,
	IDispatchMiddlewareApplicabilityEvaluator applicabilityEvaluator,
	IReadOnlySet<DispatchFeatures> enabledFeatures,
	IOptions<FilteredInvokerOptions> options,
	ILogger<FilteredDispatchMiddlewareInvoker> logger) : IDispatchMiddleware
{
	private readonly IEnumerable<IDispatchMiddleware> _allMiddleware = middleware ?? throw new ArgumentNullException(nameof(middleware));

	private readonly IDispatchMiddlewareApplicabilityEvaluator _applicabilityEvaluator =
		applicabilityEvaluator ?? throw new ArgumentNullException(nameof(applicabilityEvaluator));

	private readonly IReadOnlySet<DispatchFeatures> _enabledFeatures =
		enabledFeatures ?? throw new ArgumentNullException(nameof(enabledFeatures));

	private readonly ILogger<FilteredDispatchMiddlewareInvoker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly FilteredInvokerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <summary>
	/// Cache filtered middleware by message kind to avoid repeated filtering.
	/// </summary>
	private readonly ConcurrentDictionary<MessageKinds, IDispatchMiddleware[]> _filteredMiddlewareCache = new();

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "Message kind reflection is used for routing; message types are registered at startup.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Get filtered middleware for this message kind
		var messageKind = GetMessageKind(message);
		var applicableMiddleware = GetApplicableMiddleware(messageKind);

		if (applicableMiddleware.Length == 0)
		{
			LogNoApplicableMiddleware(messageKind);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		LogExecutingMiddleware(applicableMiddleware.Length, messageKind);

		// Execute middleware chain using optimized invoker
		var invoker = new OptimizedInvoker(applicableMiddleware);
		return await invoker.InvokeAsync(message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the message kind for the given message using reflection.
	/// </summary>
	/// <param name="message"> The message to classify. </param>
	/// <returns> The message kind. </returns>
	[RequiresUnreferencedCode("Uses reflection to check message interfaces")]
	private static MessageKinds GetMessageKind(IDispatchMessage message)
	{
		var messageType = message.GetType();
		var interfaces = messageType.GetInterfaces();

		// Check for explicit interface implementations using manual loop (avoids LINQ iterator allocation)
		if (ContainsInterfaceWithName(interfaces, "Action"))
		{
			return MessageKinds.Action;
		}

		if (ContainsInterfaceWithName(interfaces, "Event"))
		{
			return MessageKinds.Event;
		}

		if (ContainsInterfaceWithName(interfaces, "Document"))
		{
			return MessageKinds.Document;
		}

		// Default classification based on naming conventions
		var typeName = messageType.Name;
		if (typeName.EndsWith("Command", StringComparison.Ordinal) || typeName.EndsWith("Action", StringComparison.Ordinal))
		{
			return MessageKinds.Action;
		}

		if (typeName.EndsWith("Event", StringComparison.Ordinal) || typeName.EndsWith("Notification", StringComparison.Ordinal))
		{
			return MessageKinds.Event;
		}

		if (typeName.EndsWith("Document", StringComparison.Ordinal) || typeName.EndsWith("Query", StringComparison.Ordinal))
		{
			return MessageKinds.Document;
		}

		return MessageKinds.Action; // Default to Action for unknown types
	}

	/// <summary>
	/// Checks if any interface name contains the specified substring.
	/// Uses manual loop to avoid LINQ iterator allocation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ContainsInterfaceWithName(Type[] interfaces, string nameSubstring)
	{
		foreach (var iface in interfaces)
		{
			if (iface.Name.Contains(nameSubstring, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets applicable middleware for the specified message kind, using caching for performance.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <returns> Array of applicable middleware. </returns>
	private IDispatchMiddleware[] GetApplicableMiddleware(MessageKinds messageKind)
	{
		if (_options.EnableCaching)
		{
			return _filteredMiddlewareCache.GetOrAdd(messageKind, FilterMiddleware);
		}

		return FilterMiddleware(messageKind);
	}

	/// <summary>
	/// Filters middleware based on message kind and enabled features.
	/// </summary>
	/// <param name="messageKind"> The message kind to filter for. </param>
	/// <returns> Array of applicable middleware. </returns>
	private IDispatchMiddleware[] FilterMiddleware(MessageKinds messageKind)
	{
		// Pre-size list based on source count to avoid resizing allocations
		var initialCapacity = _allMiddleware.TryGetNonEnumeratedCount(out var count) ? count : 8;
		var applicableMiddleware = new List<IDispatchMiddleware>(initialCapacity);

		foreach (var middleware in _allMiddleware)
		{
			try
			{
				// Use evaluator to check applicability with features
				var middlewareType = middleware.GetType();
				var isApplicable = _applicabilityEvaluator.IsApplicable(middlewareType, messageKind, _enabledFeatures);

				if (isApplicable)
				{
					applicableMiddleware.Add(middleware);
					LogMiddlewareApplicable(middlewareType.Name, messageKind);
				}
				else
				{
					LogMiddlewareNotApplicable(middlewareType.Name, messageKind);
				}
			}
			catch (Exception ex)
			{
				LogApplicabilityEvaluationError(middleware.GetType().Name, ex);

				// Include middleware if configured to do so on error
				if (_options.IncludeMiddlewareOnFilterError)
				{
					applicableMiddleware.Add(middleware);
				}
			}
		}

		return [.. applicableMiddleware];
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.NoApplicableMiddleware, LogLevel.Debug,
		"No applicable middleware found for message kind {MessageKind}")]
	private partial void LogNoApplicableMiddleware(MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.ExecutingMiddleware, LogLevel.Trace,
		"Executing {MiddlewareCount} middleware components for message kind {MessageKind}")]
	private partial void LogExecutingMiddleware(int middlewareCount, MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.MiddlewareApplicable, LogLevel.Trace,
		"Middleware {MiddlewareType} is applicable for message kind {MessageKind}")]
	private partial void LogMiddlewareApplicable(string middlewareType, MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.MiddlewareNotApplicable, LogLevel.Trace,
		"Middleware {MiddlewareType} is not applicable for message kind {MessageKind}")]
	private partial void LogMiddlewareNotApplicable(string middlewareType, MessageKinds messageKind);

	[LoggerMessage(DeliveryEventId.ApplicabilityEvaluationError, LogLevel.Warning,
		"Error evaluating applicability for middleware {MiddlewareType}")]
	private partial void LogApplicabilityEvaluationError(string middlewareType, Exception ex);

	/// <summary>
	/// Optimized middleware invoker that executes a pre-filtered array of middleware.
	/// </summary>
	private sealed class OptimizedInvoker(IDispatchMiddleware[] middleware)
	{
		private readonly int _middlewareCount = middleware.Length;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Fast path for no middleware
			if (_middlewareCount == 0)
			{
				return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			}

			// Use struct-based state to minimize allocations
			var state = new InvokerState
			{
				Middleware = middleware,
				Message = message,
				Context = context,
				NextDelegate = nextDelegate,
				CancellationToken = cancellationToken,
				CurrentIndex = 0,
			};

			return await ExecuteNextAsync(state).ConfigureAwait(false);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async ValueTask<IMessageResult> ExecuteNextAsync(InvokerState state)
		{
			if (state.CurrentIndex >= state.Middleware.Length)
			{
				return await state.NextDelegate(state.Message, state.Context, state.CancellationToken)
					.ConfigureAwait(false);
			}

			var currentMiddleware = state.Middleware[state.CurrentIndex];
			var nextIndex = state.CurrentIndex + 1;

			// Create continuation delegate
			var continuation = new DispatchRequestDelegate(async (msg, ctx, ct) =>
			{
				var nextState = new InvokerState
				{
					Middleware = state.Middleware,
					Message = msg,
					Context = ctx,
					NextDelegate = state.NextDelegate,
					CancellationToken = ct,
					CurrentIndex = nextIndex,
				};

				return await ExecuteNextAsync(nextState).ConfigureAwait(false);
			});

			return await currentMiddleware.InvokeAsync(state.Message, state.Context, continuation, state.CancellationToken)
				.ConfigureAwait(false);
		}

		/// <summary>
		/// State struct to minimize allocations during middleware invocation.
		/// </summary>
		private struct InvokerState
		{
			public IDispatchMiddleware[] Middleware { get; set; }

			public IDispatchMessage Message { get; set; }

			public IMessageContext Context { get; set; }

			public DispatchRequestDelegate NextDelegate { get; set; }

			public CancellationToken CancellationToken { get; set; }

			public int CurrentIndex { get; set; }
		}
	}
}
