// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// High-performance dispatcher optimized for 1M+ msg/sec throughput and &lt;1μs P50 latency. Uses cached type metadata, direct field
/// access, and minimizes heap allocations in hot paths.
/// </summary>
/// <remarks>
/// <para>
/// The dispatcher resolves transport context <em>before</em> middleware invocation to ensure
/// transport-specific pipeline profiles can be selected correctly. This ordering is critical:
/// </para>
/// <list type="number">
/// <item><description>Resolve transport binding from context (via <see cref="ITransportContextProvider"/>)</description></item>
/// <item><description>Make binding available for pipeline profile resolution</description></item>
/// <item><description>Execute middleware chain with resolved context</description></item>
/// </list>
/// </remarks>
public sealed class Dispatcher(
	IDispatchMiddlewareInvoker? middlewareInvoker = null,
	FinalDispatchHandler? finalHandler = null,
	ITransportContextProvider? transportContextProvider = null,
	IServiceProvider? serviceProvider = null,
	LocalMessageBus? localMessageBus = null,
	IDictionary<string, IMessageBusOptions>? busOptionsMap = null,
	IDispatchRouter? dispatchRouter = null,
	IOptions<DispatchOptions>? dispatchOptions = null) : IDispatcher, IDirectLocalDispatcher
{
	/// <inheritdoc />
	public IServiceProvider? ServiceProvider => serviceProvider;

	/// <summary>
	/// Context property key for storing the resolved transport binding.
	/// </summary>
	/// <remarks>
	/// Middleware and pipeline profile resolvers can access this property to
	/// obtain the transport binding that received the message.
	/// </remarks>
	internal const string TransportBindingContextKey = "Excalibur.Dispatch.TransportBinding";

	internal const string ReturnCancelledResultContextKey = "Dispatch:ReturnCancelledResult";
	internal const string ResultContextKey = "Dispatch:Result";
	internal const string CacheHitContextKey = "Dispatch:CacheHit";
	internal const string LocalBusName = "local";

	private static readonly Task<IMessageResult> CancelledResultTask =
		Task.FromResult<IMessageResult>(Messaging.MessageResult.Cancelled());

	private static readonly Task<IMessageResult> DirectLocalSuccessResultTask =
		Task.FromResult(SimpleMessageResult.SuccessResult);

	private static readonly Task<IMessageResult> DirectLocalSuccessCacheHitResultTask =
		Task.FromResult(SimpleMessageResult.SuccessCacheHitResult);
	[ThreadStatic] private static Dispatcher? s_cachedDispatchInfoDispatcher;
	[ThreadStatic] private static Type? s_cachedDispatchInfoType;
	[ThreadStatic] private static MessageDispatchInfo s_cachedDispatchInfo;
	[ThreadStatic] private static bool s_cachedDispatchInfoInitialized;

	private readonly ConcurrentDictionary<Type, bool> _middlewareBypassCache = new();

	/// <summary>
	/// Combined per-type dispatch info cache used across dispatch paths to avoid repeated type checks
	/// and per-dispatch applicability lookups.
	/// </summary>
	private readonly ConcurrentDictionary<Type, MessageDispatchInfo> _messageDispatchInfoCache = new();
	private readonly DispatchMiddlewareInvoker? _concreteMiddlewareInvoker = middlewareInvoker as DispatchMiddlewareInvoker;
	private readonly bool _canBypassAllMiddleware = middlewareInvoker is DispatchMiddlewareInvoker { HasMiddleware: false };
	private readonly bool _directLocalActionPathEnabled = IsDirectLocalActionPathEnabled(middlewareInvoker, busOptionsMap);
	private readonly bool _correlationEnabled = dispatchOptions?.Value.Features.EnableCorrelation ?? true;
	private readonly bool _ambientContextFlowEnabled = dispatchOptions?.Value.CrossCutting.Observability.EnableContextFlow ?? true;

	private readonly DirectLocalContextInitializationProfile _directLocalContextInitializationProfile =
		dispatchOptions?.Value.CrossCutting.Performance.DirectLocalContextInitialization ??
		DirectLocalContextInitializationProfile.Lean;

	private readonly bool _emitDirectLocalResultMetadata =
		dispatchOptions?.Value.CrossCutting.Performance.EmitDirectLocalResultMetadata ?? false;

	// PERF-7: Cache the IMessageContextFactory at construction time to avoid per-dispatch DI lookup.
	// The factory is a singleton, so resolving it once is safe and eliminates ~10-15ns of GetService overhead per dispatch.
	// Use non-generic GetService + safe cast to avoid InvalidCastException with faked IServiceProvider in tests.
	private readonly IMessageContextFactory? _cachedContextFactory =
		serviceProvider?.GetService(typeof(IMessageContextFactory)) as IMessageContextFactory;

	// PERF: Cache terminal handler delegate once to avoid method-group conversion on every dispatch.
	private readonly Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>? _finalHandlerDelegate =
		finalHandler is null ? null : finalHandler.HandleAsync;

	/// <summary>
	/// Dispatches a message through the pipeline with high-performance optimizations.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message to dispatch. </typeparam>
	/// <param name="message"> The message to dispatch. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the dispatch result. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Task<IMessageResult> DispatchAsync<TMessage>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		if (middlewareInvoker == null || finalHandler == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		if (cancellationToken.IsCancellationRequested && ShouldReturnCancelledResult(context))
		{
			return CancelledResultTask;
		}

		var messageType = message.GetType();

		// PERF: Single combined cache lookup replaces separate CanBypassMiddleware + ExpectsResponse +
		// type-check calls. Saves ~15-20ns by avoiding two ConcurrentDictionary accesses.
		var dispatchInfo = GetMessageDispatchInfo(messageType);

		if (_directLocalActionPathEnabled &&
		    localMessageBus is not null &&
		    dispatchInfo.CanBypassMiddleware &&
		    dispatchRouter is null &&
		    context.RoutingDecision is null)
		{
			if (dispatchInfo.IsAction)
			{
				var action = (IDispatchAction)message;
				if (dispatchInfo.DirectLocalNoResponseEligible &&
				    TryDispatchUltraLocalNoResponseFast(
					    message,
					    action,
					    context,
					    dispatchInfo.DirectLocalNoResponseInvoker!,
					    out var promotedTask,
					    cancellationToken))
				{
					return promotedTask;
				}

				if (dispatchInfo.DirectLocalTypedEligible &&
				    TryDispatchUltraLocalUntypedResponseFast(
					    message,
					    action,
					    context,
					    dispatchInfo.DirectLocalWithResponseInvoker!,
					    out promotedTask,
					    cancellationToken))
				{
					return promotedTask;
				}

				if (dispatchInfo.ExpectsResponse)
				{
					return DispatchDirectLocalActionUntypedWithResponseAsync(message, action, context, cancellationToken);
				}

				return DispatchDirectLocalActionAsync(message, action, context, cancellationToken);
			}

			if (dispatchInfo.IsEvent)
			{
				return DispatchDirectLocalEventAsync(message, (IDispatchEvent)message, context, cancellationToken);
			}
		}

		var routingTask = EnsureRoutingDecisionAsync(message, context, cancellationToken);
		if (!routingTask.IsCompletedSuccessfully)
		{
			return DispatchWithPreRoutingAsync(message, context, routingTask, dispatchInfo, cancellationToken);
		}

		var routingDecision = routingTask.Result;
		if (routingDecision is { IsSuccess: false } failedDecision)
		{
			return Task.FromResult(CreateRoutingFailureResult(context, failedDecision));
		}

		if (_directLocalActionPathEnabled &&
		    localMessageBus is not null &&
		    dispatchInfo.CanBypassMiddleware &&
		    IsLocalRoute(context))
		{
			if (dispatchInfo.IsAction)
			{
				var action = (IDispatchAction)message;
				if (dispatchInfo.DirectLocalNoResponseEligible &&
				    TryDispatchUltraLocalNoResponseFast(
					    message,
					    action,
					    context,
					    dispatchInfo.DirectLocalNoResponseInvoker!,
					    out var promotedTask,
					    cancellationToken))
				{
					return promotedTask;
				}

				if (dispatchInfo.DirectLocalTypedEligible &&
				    TryDispatchUltraLocalUntypedResponseFast(
					    message,
					    action,
					    context,
					    dispatchInfo.DirectLocalWithResponseInvoker!,
					    out promotedTask,
					    cancellationToken))
				{
					return promotedTask;
				}

				if (dispatchInfo.ExpectsResponse)
				{
					return DispatchDirectLocalActionUntypedWithResponseAsync(message, action, context, cancellationToken);
				}

				return DispatchDirectLocalActionAsync(message, action, context, cancellationToken);
			}

			if (dispatchInfo.IsEvent)
			{
				return DispatchDirectLocalEventAsync(message, (IDispatchEvent)message, context, cancellationToken);
			}
		}

		return DispatchOptimizedAsync(message, context, dispatchInfo.CanBypassMiddleware, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action through the pipeline and returns the response with high-performance optimizations.
	/// </summary>
	/// <typeparam name="TMessage"> The type of action to dispatch. </typeparam>
	/// <typeparam name="TResponse"> The type of response expected. </typeparam>
	/// <param name="message"> The action to dispatch. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the dispatch result with response. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		if (middlewareInvoker == null || finalHandler == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		if (cancellationToken.IsCancellationRequested && ShouldReturnCancelledResult(context))
		{
			return CancelledResultTaskCache<TResponse>.Task;
		}

		var messageType = message.GetType();
		var dispatchInfo = GetMessageDispatchInfo(messageType);
		var canBypass = dispatchInfo.CanBypassMiddleware;

		if (_directLocalActionPathEnabled &&
		    localMessageBus is not null &&
		    canBypass &&
		    dispatchRouter is null &&
		    context.RoutingDecision is null)
		{
			if (dispatchInfo.DirectLocalTypedEligible &&
			    TryDispatchUltraLocalTypedFast<TMessage, TResponse>(
				    message,
				    context,
				    dispatchInfo.DirectLocalWithResponseInvoker!,
				    out var promotedTask,
				    cancellationToken))
			{
				return promotedTask;
			}

			return DispatchDirectLocalActionWithResponseAsync<TMessage, TResponse>(message, context, cancellationToken);
		}

		var routingTask = EnsureRoutingDecisionAsync(message, context, cancellationToken);
		if (!routingTask.IsCompletedSuccessfully)
		{
			return DispatchWithPreRoutingAsync<TMessage, TResponse>(message, context, routingTask, dispatchInfo, cancellationToken);
		}

		var routingDecision = routingTask.Result;
		if (routingDecision is { IsSuccess: false } failedDecision)
		{
			return Task.FromResult(CreateRoutingFailureResult<TResponse>(context, failedDecision));
		}

		if (_directLocalActionPathEnabled && localMessageBus is not null && IsLocalRoute(context))
		{
			if (!canBypass)
			{
				return DispatchOptimizedWithResponseAsync<TMessage, TResponse>(message, context, canBypassMiddleware: false, cancellationToken);
			}

			if (dispatchInfo.DirectLocalTypedEligible &&
			    TryDispatchUltraLocalTypedFast<TMessage, TResponse>(
				    message,
				    context,
				    dispatchInfo.DirectLocalWithResponseInvoker!,
				    out var promotedTask,
				    cancellationToken))
			{
				return promotedTask;
			}

			return DispatchDirectLocalActionWithResponseAsync<TMessage, TResponse>(message, context, cancellationToken);
		}

		return DispatchOptimizedWithResponseAsync<TMessage, TResponse>(message, context, canBypass, cancellationToken);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueTask DispatchLocalAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(message);

		if (cancellationToken.IsCancellationRequested)
		{
			return ValueTask.FromCanceled(cancellationToken);
		}

		if (!CanUseUltraLocalPath())
		{
			return new ValueTask(DispatchLocalFallbackAsync(message, cancellationToken));
		}

		if (localMessageBus.TryInvokeUltraLocalNoResponse(
			    message,
			    cancellationToken,
			    out var ultraLocalInvocation,
			    out var requiresContext))
		{
			return requiresContext
				? ValueTask.FromException(new InvalidOperationException(
					$"Direct local dispatch for {message.GetType().FullName} requires a context-bound path."))
				: (ultraLocalInvocation.IsCompletedSuccessfully
					? ValueTask.CompletedTask
					: AwaitUltraLocalNoResponseAsync(ultraLocalInvocation));
		}

		if (requiresContext &&
		    TryInvokeDirectNoResponseWithLazyContext(
			    message,
			    cancellationToken,
			    out var contextBoundInvocation,
			    out var rentedContext,
			    out var contextFactory))
		{
			if (contextBoundInvocation.IsCompletedSuccessfully)
			{
				ReturnDispatchContext(contextFactory, rentedContext);
				return ValueTask.CompletedTask;
			}

			return AwaitUltraLocalNoResponseWithContextAsync(contextBoundInvocation, rentedContext, contextFactory);
		}

		var messageType = message.GetType();
		return ValueTask.FromException(CreateMissingLocalHandlerException(messageType));
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ValueTask<TResponse?> DispatchLocalAsync<TMessage, TResponse>(
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(message);

		if (cancellationToken.IsCancellationRequested)
		{
			return ValueTask.FromCanceled<TResponse?>(cancellationToken);
		}

		if (!CanUseUltraLocalPath())
		{
			return new ValueTask<TResponse?>(DispatchLocalFallbackWithResponseAsync<TMessage, TResponse>(message, cancellationToken));
		}

		if (localMessageBus.TryInvokeUltraLocal(
			    message,
			    cancellationToken,
			    out var ultraLocalInvocation,
			    out var requiresContext))
		{
			if (!requiresContext)
			{
				if (ultraLocalInvocation.IsCompletedSuccessfully)
				{
					return new ValueTask<TResponse?>(CastUltraLocalResponse<TResponse>(ultraLocalInvocation.Result));
				}

				return AwaitUltraLocalResponseAsync<TResponse>(ultraLocalInvocation);
			}
		}

		if (requiresContext &&
		    TryInvokeDirectWithLazyContext(
			    message,
			    cancellationToken,
			    out var contextBoundInvocation,
			    out var rentedContext,
			    out var contextFactory))
		{
			if (contextBoundInvocation.IsCompletedSuccessfully)
			{
				try
				{
					return new ValueTask<TResponse?>(CastUltraLocalResponse<TResponse>(contextBoundInvocation.Result));
				}
				finally
				{
					ReturnDispatchContext(contextFactory, rentedContext);
				}
			}

			return AwaitUltraLocalResponseWithContextAsync<TResponse>(contextBoundInvocation, rentedContext, contextFactory);
		}

		var messageType = message.GetType();
		return ValueTask.FromException<TResponse?>(CreateMissingLocalHandlerException(messageType));
	}

	private async Task<IMessageResult> DispatchWithPreRoutingAsync<TMessage>(
		TMessage message,
		IMessageContext context,
		ValueTask<RoutingDecision?> routingTask,
		MessageDispatchInfo dispatchInfo,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		var routingDecision = await routingTask.ConfigureAwait(false);
		if (routingDecision is { IsSuccess: false } failedDecision)
		{
			return CreateRoutingFailureResult(context, failedDecision);
		}

		// PERF: Reuse dispatch metadata resolved by the caller.
		var canBypass = dispatchInfo.CanBypassMiddleware;

		if (_directLocalActionPathEnabled &&
		    localMessageBus is not null &&
		    canBypass &&
		    dispatchInfo.IsAction &&
		    message is IDispatchAction action &&
		    IsLocalRoute(context))
		{
			if (dispatchInfo.DirectLocalNoResponseEligible &&
			    TryDispatchUltraLocalNoResponseFast(
				    message,
				    action,
				    context,
				    dispatchInfo.DirectLocalNoResponseInvoker!,
				    out var promotedTask,
				    cancellationToken))
			{
				return await promotedTask.ConfigureAwait(false);
			}

			if (dispatchInfo.DirectLocalTypedEligible &&
			    TryDispatchUltraLocalUntypedResponseFast(
				    message,
				    action,
				    context,
				    dispatchInfo.DirectLocalWithResponseInvoker!,
				    out promotedTask,
				    cancellationToken))
			{
				return await promotedTask.ConfigureAwait(false);
			}

			if (dispatchInfo.ExpectsResponse)
			{
				return await DispatchDirectLocalActionUntypedWithResponseAsync(message, action, context, cancellationToken)
					.ConfigureAwait(false);
			}

			return await DispatchDirectLocalActionAsync(message, action, context, cancellationToken).ConfigureAwait(false);
		}

		if (_directLocalActionPathEnabled &&
		    localMessageBus is not null &&
		    canBypass &&
		    dispatchInfo.IsEvent &&
		    message is IDispatchEvent evt &&
		    IsLocalRoute(context))
		{
			return await DispatchDirectLocalEventAsync(message, evt, context, cancellationToken).ConfigureAwait(false);
		}

		return await DispatchOptimizedAsync(message, context, canBypass, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IMessageResult<TResponse>> DispatchWithPreRoutingAsync<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		ValueTask<RoutingDecision?> routingTask,
		MessageDispatchInfo dispatchInfo,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		var routingDecision = await routingTask.ConfigureAwait(false);
		if (routingDecision is { IsSuccess: false } failedDecision)
		{
			return CreateRoutingFailureResult<TResponse>(context, failedDecision);
		}

		if (_directLocalActionPathEnabled && localMessageBus is not null && IsLocalRoute(context))
		{
			if (!dispatchInfo.CanBypassMiddleware)
			{
				return await DispatchOptimizedWithResponseAsync<TMessage, TResponse>(message, context, canBypassMiddleware: false, cancellationToken)
					.ConfigureAwait(false);
			}

			if (dispatchInfo.DirectLocalTypedEligible &&
			    TryDispatchUltraLocalTypedFast<TMessage, TResponse>(
				    message,
				    context,
				    dispatchInfo.DirectLocalWithResponseInvoker!,
				    out var promotedTask,
				    cancellationToken))
			{
				return await promotedTask.ConfigureAwait(false);
			}

			return await DispatchDirectLocalActionWithResponseAsync<TMessage, TResponse>(message, context, cancellationToken)
				.ConfigureAwait(false);
		}

		return await DispatchOptimizedWithResponseAsync<TMessage, TResponse>(
				message,
				context,
				dispatchInfo.CanBypassMiddleware,
				cancellationToken)
			.ConfigureAwait(false);
	}

	private static async ValueTask AwaitUltraLocalNoResponseAsync(ValueTask invocation)
	{
		await invocation.ConfigureAwait(false);
	}

	private static async ValueTask<TResponse?> AwaitUltraLocalResponseAsync<TResponse>(ValueTask<object?> invocation)
	{
		var result = await invocation.ConfigureAwait(false);
		return CastUltraLocalResponse<TResponse>(result);
	}

	private static async ValueTask AwaitUltraLocalNoResponseWithContextAsync(
		ValueTask invocation,
		IMessageContext context,
		IMessageContextFactory? factory)
	{
		try
		{
			await invocation.ConfigureAwait(false);
		}
		finally
		{
			ReturnDispatchContext(factory, context);
		}
	}

	private static async ValueTask<TResponse?> AwaitUltraLocalResponseWithContextAsync<TResponse>(
		ValueTask<object?> invocation,
		IMessageContext context,
		IMessageContextFactory? factory)
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			return CastUltraLocalResponse<TResponse>(result);
		}
		finally
		{
			ReturnDispatchContext(factory, context);
		}
	}

	private static TResponse? CastUltraLocalResponse<TResponse>(object? value)
	{
		if (value is null)
		{
			return default;
		}

		if (value is TResponse typed)
		{
			return typed;
		}

		throw new InvalidOperationException(
			$"Direct local dispatch returned {value.GetType().FullName}, expected {typeof(TResponse).FullName}.");
	}

	private async Task DispatchLocalFallbackAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
		where TMessage : IDispatchAction
	{
		var (context, contextFactory) = CreateDispatchContext();
		try
		{
			var result = await DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			ThrowIfFailed(result);
		}
		finally
		{
			ReturnDispatchContext(contextFactory, context);
		}
	}

	private async Task<TResponse?> DispatchLocalFallbackWithResponseAsync<TMessage, TResponse>(
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		var (context, contextFactory) = CreateDispatchContext();
		try
		{
			var result = await DispatchAsync<TMessage, TResponse>(message, context, cancellationToken).ConfigureAwait(false);
			ThrowIfFailed(result);
			return result.ReturnValue;
		}
		finally
		{
			ReturnDispatchContext(contextFactory, context);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CanUseUltraLocalPath()
	{
		return _directLocalActionPathEnabled &&
		       localMessageBus is not null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryInvokeDirectWithLazyContext<TMessage>(
		TMessage message,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation,
		out IMessageContext context,
		out IMessageContextFactory? contextFactory)
		where TMessage : IDispatchAction
	{
		var dispatchContext = CreateDispatchContext();
		context = dispatchContext.Context;
		contextFactory = dispatchContext.Factory;

		InitializeDirectLocalContext(message, context);
		if (localMessageBus!.TryInvokeDirect(message, context, cancellationToken, out invocation))
		{
			return true;
		}

		ReturnDispatchContext(contextFactory, context);
		context = null!;
		contextFactory = null;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryInvokeDirectNoResponseWithLazyContext<TMessage>(
		TMessage message,
		CancellationToken cancellationToken,
		out ValueTask invocation,
		out IMessageContext context,
		out IMessageContextFactory? contextFactory)
		where TMessage : IDispatchAction
	{
		var dispatchContext = CreateDispatchContext();
		context = dispatchContext.Context;
		contextFactory = dispatchContext.Factory;

		InitializeDirectLocalContext(message, context);
		if (localMessageBus!.TryInvokeDirectNoResponse(message, context, cancellationToken, out invocation))
		{
			return true;
		}

		ReturnDispatchContext(contextFactory, context);
		context = null!;
		contextFactory = null;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private (IMessageContext Context, IMessageContextFactory? Factory) CreateDispatchContext()
	{
		// PERF-7: Use cached factory instead of per-dispatch DI lookup.
		var context = _cachedContextFactory?.CreateContext() ?? new MessageContext();
		return (context, _cachedContextFactory);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ReturnDispatchContext(IMessageContextFactory? factory, IMessageContext context)
	{
		factory?.Return(context);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ThrowIfFailed(IMessageResult result)
	{
		if (result.Succeeded)
		{
			return;
		}

		throw new InvalidOperationException(result.ErrorMessage ?? "Direct local dispatch failed.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryDispatchUltraLocalNoResponseFast<TMessage>(
		TMessage message,
		IDispatchAction action,
		IMessageContext context,
		Func<IDispatchAction, CancellationToken, ValueTask> noResponseInvoker,
		out Task<IMessageResult> task,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				task = CancelledResultTask;
				return true;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = MessageContextHolder.Current;
		var useAmbientFlow = _ambientContextFlowEnabled;
		if (useAmbientFlow)
		{
			MessageContextHolder.Current = context;
		}
		try
		{
			InitializeDirectLocalContext(message, context);
			var invocation = noResponseInvoker(action, cancellationToken);

			task = invocation.IsCompletedSuccessfully
				? DirectLocalSuccessResultTask
				: AwaitDirectLocalNoResponseAsync(invocation, context);
			return true;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			task = CancelledResultTask;
			return true;
		}
		catch (Exception ex)
		{
			task = Task.FromResult(CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed"));
			return true;
		}
		finally
		{
			if (useAmbientFlow)
			{
				MessageContextHolder.Current = previous;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryDispatchUltraLocalUntypedResponseFast<TMessage>(
		TMessage message,
		IDispatchAction action,
		IMessageContext context,
		Func<IDispatchAction, CancellationToken, ValueTask<object?>> withResponseInvoker,
		out Task<IMessageResult> task,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				task = CancelledResultTask;
				return true;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = MessageContextHolder.Current;
		var useAmbientFlow = _ambientContextFlowEnabled;
		if (useAmbientFlow)
		{
			MessageContextHolder.Current = context;
		}
		try
		{
			InitializeDirectLocalContext(message, context);
			var invocation = withResponseInvoker(action, cancellationToken);

			if (invocation.IsCompletedSuccessfully)
			{
				TrySetContextResult(context, invocation.Result);
				task = DirectLocalSuccessResultTask;
				return true;
			}

			task = AwaitDirectLocalUntypedWithResponseAsync(invocation, context);
			return true;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			task = CancelledResultTask;
			return true;
		}
		catch (Exception ex)
		{
			task = Task.FromResult(CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed"));
			return true;
		}
		finally
		{
			if (useAmbientFlow)
			{
				MessageContextHolder.Current = previous;
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryDispatchUltraLocalTypedFast<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		Func<IDispatchAction, CancellationToken, ValueTask<object?>> withResponseInvoker,
		out Task<IMessageResult<TResponse>> task,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				task = CancelledResultTaskCache<TResponse>.Task;
				return true;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = MessageContextHolder.Current;
		var useAmbientFlow = _ambientContextFlowEnabled;
		if (useAmbientFlow)
		{
			MessageContextHolder.Current = context;
		}
		try
		{
			InitializeDirectLocalContext(message, context);
			var invocation = withResponseInvoker(message, cancellationToken);
			task = invocation.IsCompletedSuccessfully
				? Task.FromResult(CreateDirectLocalTypedSuccessResult<TResponse>(invocation.Result, context))
				: AwaitDirectLocalWithResponseAsync<TResponse>(invocation, context);
			return true;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			task = CancelledResultTaskCache<TResponse>.Task;
			return true;
		}
		catch (Exception ex)
		{
			task = Task.FromResult(CreateDirectLocalFailureResult<TResponse>(ex, context, "Direct local dispatch failed"));
			return true;
		}
		finally
		{
			if (useAmbientFlow)
			{
				MessageContextHolder.Current = previous;
			}
		}
	}

	private static InvalidOperationException CreateMissingLocalHandlerException(Type actionType)
	{
		return new InvalidOperationException($"No handler registered for action {actionType.Name}");
	}

	/// <summary>
	/// Optimized dispatch path using cached type metadata and minimal allocations.
	/// Sets the ambient context at the Dispatcher level to ensure it's always available,
	/// regardless of middleware configuration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Transport binding resolution happens FIRST, before any middleware execution.
	/// This ensures pipeline profile resolution can consider the transport source.
	/// </para>
	/// <para>
	/// <strong>Performance note:</strong> This method avoids the <c>async</c> keyword to
	/// prevent state machine allocation on synchronous completion paths. When the middleware
	/// chain and handler complete synchronously (the common case for in-process handlers),
	/// no <see cref="Task"/> allocation occurs beyond the cached result.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Task<IMessageResult> DispatchOptimizedAsync<TMessage>(
		TMessage message,
		IMessageContext context,
		bool canBypassMiddleware,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);

		try
		{
			InitializeDispatchContext(message, context);
			var finalHandlerDelegate = _finalHandlerDelegate ?? throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
			var invocation = canBypassMiddleware
				? finalHandlerDelegate(message, context, cancellationToken)
				: middlewareInvoker!.InvokeAsync(message, context, finalHandlerDelegate, cancellationToken);

			if (invocation.IsCompletedSuccessfully)
			{
				PopAmbientContext(previous);
				return GetCachedResultTask(invocation.Result);
			}

			// IMPORTANT: Pop ambient context HERE (same execution context where Push happened)
			// before entering the async continuation. AsyncLocal writes in async methods only
			// affect the child EC copy — they don't propagate back to the caller's EC.
			PopAmbientContext(previous);
			return AwaitDispatchOptimizedAsync(invocation, context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			PopAmbientContext(previous);
			return CancelledResultTask;
		}
		catch
		{
			PopAmbientContext(previous);
			throw;
		}
	}

	/// <summary>
	/// Async continuation for <see cref="DispatchOptimizedAsync{TMessage}"/> when the invocation
	/// does not complete synchronously. Separated to avoid state machine allocation on the fast path.
	/// </summary>
	/// <remarks>
	/// The ambient context is already popped by the caller (non-async method) before this method
	/// is entered. The middleware continuation still has the correct ambient context in its captured
	/// <see cref="System.Threading.ExecutionContext"/>.
	/// </remarks>
	private async Task<IMessageResult> AwaitDispatchOptimizedAsync(
		ValueTask<IMessageResult> invocation,
		IMessageContext context)
	{
		try
		{
			return await invocation.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
	}

	/// <summary>
	/// Optimized dispatch path for messages with responses.
	/// Sets the ambient context at the Dispatcher level to ensure it's always available,
	/// regardless of middleware configuration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Transport binding resolution happens FIRST, before any middleware execution.
	/// This ensures pipeline profile resolution can consider the transport source.
	/// </para>
	/// <para>
	/// <strong>Performance note:</strong> This method avoids the <c>async</c> keyword to
	/// prevent state machine allocation on synchronous completion paths.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException"> </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Task<IMessageResult<TResponse>> DispatchOptimizedWithResponseAsync<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		bool canBypassMiddleware,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTaskCache<TResponse>.Task;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);

		try
		{
			InitializeDispatchContext(message, context);
			var finalHandlerDelegate = _finalHandlerDelegate ?? throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
			var invocation = canBypassMiddleware
				? finalHandlerDelegate(message, context, cancellationToken)
				: middlewareInvoker!.InvokeAsync(message, context, finalHandlerDelegate, cancellationToken);

			if (invocation.IsCompletedSuccessfully)
			{
				PopAmbientContext(previous);
				return Task.FromResult(ConvertResult<TResponse>(invocation.Result, context));
			}

			// IMPORTANT: Pop ambient context HERE (same EC where Push happened).
			PopAmbientContext(previous);
			return AwaitDispatchOptimizedWithResponseAsync<TResponse>(invocation, context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			PopAmbientContext(previous);
			return CancelledResultTaskCache<TResponse>.Task;
		}
		catch
		{
			PopAmbientContext(previous);
			throw;
		}
	}

	/// <summary>
	/// Async continuation for <see cref="DispatchOptimizedWithResponseAsync{TMessage,TResponse}"/> when the invocation
	/// does not complete synchronously.
	/// </summary>
	private async Task<IMessageResult<TResponse>> AwaitDispatchOptimizedWithResponseAsync<TResponse>(
		ValueTask<IMessageResult> invocation,
		IMessageContext context)
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			return ConvertResult<TResponse>(result, context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return MessageResultOfT<TResponse>.Cancelled();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void InitializeDispatchContext<TMessage>(TMessage message, IMessageContext context)
		where TMessage : IDispatchMessage
	{
		// Resolve transport binding before middleware so profile resolution can consider transport origin.
		if (transportContextProvider != null)
		{
			var transportBinding = transportContextProvider.GetTransportBinding(context);
			if (transportBinding != null)
			{
				context.Items[TransportBindingContextKey] = transportBinding;
			}
		}

		// PERF-6: Use lazy generation for CorrelationId and CausationId.
		if (_correlationEnabled)
		{
			if (context is MessageContext mc)
			{
				mc.MarkForLazyCorrelation();
				mc.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				if (context.CausationId is null && context.CorrelationId is not null)
				{
					context.CausationId = context.CorrelationId;
				}
			}
		}

		context.Message = message;

		if (context.MessageType is null)
		{
			var messageType = message.GetType();
			context.MessageType = MessageTypeCache.GetTypeName(messageType);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void InitializeDirectLocalContext<TMessage>(TMessage message, IMessageContext context)
		where TMessage : IDispatchMessage
	{
		context.Message = message;
		if (_correlationEnabled)
		{
			if (context is MessageContext messageContext)
			{
				messageContext.MarkForLazyCorrelation();
				messageContext.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				if (context.CausationId is null && context.CorrelationId is not null)
				{
					context.CausationId = context.CorrelationId;
				}
			}
		}

		if (_directLocalContextInitializationProfile == DirectLocalContextInitializationProfile.Lean)
		{
			return;
		}

		if (context.MessageType is null)
		{
			context.MessageType = MessageTypeCache.GetTypeName(message.GetType());
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask<RoutingDecision?> EnsureRoutingDecisionAsync<TMessage>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		if (TryGetUsableRoutingDecision(context, out var existingDecision))
		{
			return ValueTask.FromResult<RoutingDecision?>(existingDecision);
		}

		if (dispatchRouter is null)
		{
			return ValueTask.FromResult<RoutingDecision?>(null);
		}

		return ResolveRoutingDecisionAsync(dispatchRouter, message, context, cancellationToken);
	}

	private static async ValueTask<RoutingDecision?> ResolveRoutingDecisionAsync(
		IDispatchRouter router,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		var routeTask = router.RouteAsync(message, context, cancellationToken);
		var decision = routeTask.IsCompletedSuccessfully
			? routeTask.Result
			: await routeTask.ConfigureAwait(false);

		context.RoutingDecision = decision;
		return decision;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryGetUsableRoutingDecision(IMessageContext context, out RoutingDecision decision)
	{
		decision = context.RoutingDecision!;
		if (decision is null)
		{
			return false;
		}

		// Fake IMessageContext instances may return a dummy RoutingDecision with null/empty fields.
		// Only treat decisions as precomputed when they are semantically valid.
		return !string.IsNullOrEmpty(decision.Transport) || !string.IsNullOrEmpty(decision.FailureReason);
	}

	private static IMessageResult CreateRoutingFailureResult(IMessageContext context, RoutingDecision decision)
	{
		var problem = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Routing failed",
			Status = 404,
			Detail = $"Routing failed: {decision.FailureReason ?? "unspecified"}",
			Instance = Guid.NewGuid().ToString(),
		};

		return Messaging.MessageResult.Failure(
			problem,
			context.RoutingDecision,
			context.ValidationResult() as IValidationResult,
			context.AuthorizationResult() as IAuthorizationResult);
	}

	private static IMessageResult<TResponse> CreateRoutingFailureResult<TResponse>(
		IMessageContext context,
		RoutingDecision decision)
	{
		var problem = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Routing failed",
			Status = 404,
			Detail = $"Routing failed: {decision.FailureReason ?? "unspecified"}",
			Instance = Guid.NewGuid().ToString(),
		};

		return MessageResultOfT<TResponse>.Failure(
			problem,
			context.RoutingDecision,
			context.ValidationResult() as IValidationResult,
			context.AuthorizationResult() as IAuthorizationResult);
	}

	private Task<IMessageResult> DispatchDirectLocalActionAsync<TMessage>(
		TMessage message,
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		// PERF: Null checks removed — already validated by DispatchAsync caller.

		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);
		try
		{
			try
			{
				var hasUltraLocal = localMessageBus!.TryInvokeUltraLocalNoResponse(
					action,
					cancellationToken,
					out var ultraLocalInvocation,
					out var requiresContext);

				if (hasUltraLocal && !requiresContext)
				{
					InitializeDirectLocalContext(message, context);

					if (ultraLocalInvocation.IsCompletedSuccessfully)
					{
						return DirectLocalSuccessResultTask;
					}

					return AwaitDirectLocalNoResponseAsync(ultraLocalInvocation, context);
				}

				if (requiresContext)
				{
					InitializeDirectLocalContext(message, context);
					if (localMessageBus.TryInvokeDirectNoResponse(action, context, cancellationToken, out var invocation))
					{
						if (invocation.IsCompletedSuccessfully)
						{
							return DirectLocalSuccessResultTask;
						}

						return AwaitDirectLocalNoResponseAsync(invocation, context);
					}

					var sendTaskWithContext = localMessageBus.SendAsync(action, context, cancellationToken);
					if (sendTaskWithContext.IsCompletedSuccessfully)
					{
						return DirectLocalSuccessResultTask;
					}

					return AwaitDirectLocalSendNoResponseAsync(sendTaskWithContext, context);
				}

				InitializeDirectLocalContext(message, context);
				var sendTask = localMessageBus.SendAsync(action, context, cancellationToken);
				if (sendTask.IsCompletedSuccessfully)
				{
					return DirectLocalSuccessResultTask;
				}

				return AwaitDirectLocalSendNoResponseAsync(sendTask, context);
			}
			catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}
			catch (Exception ex)
			{
				return Task.FromResult(CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed"));
			}
		}
		finally
		{
			PopAmbientContext(previous);
		}
	}

	private Task<IMessageResult<TResponse>> DispatchDirectLocalActionWithResponseAsync<TMessage, TResponse>(
		TMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		// PERF: Null checks removed — already validated by DispatchAsync caller.

		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTaskCache<TResponse>.Task;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);
		try
		{
			try
			{
				var hasTypedUltraLocal = localMessageBus.TryInvokeUltraLocalTyped<TMessage, TResponse>(
					message,
					cancellationToken,
					out var typedUltraLocalInvocation,
					out var requiresContext);

				if (hasTypedUltraLocal && !requiresContext)
				{
					InitializeDirectLocalContext(message, context);

					if (typedUltraLocalInvocation.IsCompletedSuccessfully)
					{
						return Task.FromResult(CreateDirectLocalTypedSuccessResult<TResponse>(typedUltraLocalInvocation.Result, context));
					}

					return AwaitDirectLocalTypedWithResponseAsync<TResponse>(typedUltraLocalInvocation, context);
				}

				var hasUltraLocal = localMessageBus.TryInvokeUltraLocal(
					message,
					cancellationToken,
					out var ultraLocalInvocation,
					out requiresContext);

				if (hasUltraLocal && !requiresContext)
				{
					InitializeDirectLocalContext(message, context);

					if (ultraLocalInvocation.IsCompletedSuccessfully)
					{
						return Task.FromResult(
							CreateDirectLocalTypedSuccessResult<TResponse>(CastUltraLocalResponse<TResponse>(ultraLocalInvocation.Result),
								context));
					}

					return AwaitDirectLocalWithResponseAsync<TResponse>(ultraLocalInvocation, context);
				}

				if (requiresContext)
				{
					InitializeDirectLocalContext(message, context);
					if (localMessageBus.TryInvokeDirect(message, context, cancellationToken, out var invocation))
					{
						if (invocation.IsCompletedSuccessfully)
						{
							return Task.FromResult(CreateDirectLocalTypedSuccessResult<TResponse>(invocation.Result, context));
						}

						return AwaitDirectLocalWithResponseAsync<TResponse>(invocation, context);
					}

					var sendTaskWithContext = localMessageBus.SendAsync(message, context, cancellationToken);
					if (sendTaskWithContext.IsCompletedSuccessfully)
					{
						return Task.FromResult(CreateDirectLocalTypedSuccessResult<TResponse>(GetResultFromContext(context), context));
					}

					return AwaitDirectLocalSendWithResponseAsync<TResponse>(sendTaskWithContext, context);
				}

				InitializeDirectLocalContext(message, context);
				var sendTask = localMessageBus.SendAsync(message, context, cancellationToken);
				if (sendTask.IsCompletedSuccessfully)
				{
					return Task.FromResult(CreateDirectLocalTypedSuccessResult<TResponse>(GetResultFromContext(context), context));
				}

				return AwaitDirectLocalSendWithResponseAsync<TResponse>(sendTask, context);
			}
			catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTaskCache<TResponse>.Task;
			}
			catch (Exception ex)
			{
				return Task.FromResult(CreateDirectLocalFailureResult<TResponse>(ex, context, "Direct local dispatch failed"));
			}
		}
		finally
		{
			PopAmbientContext(previous);
		}
	}

	private Task<IMessageResult> DispatchDirectLocalActionUntypedWithResponseAsync<TMessage>(
		TMessage message,
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		// PERF: Null checks removed — already validated by DispatchAsync caller.

		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);
		try
		{
			try
			{
				var hasUltraLocal = localMessageBus.TryInvokeUltraLocal(
					action,
					cancellationToken,
					out var ultraLocalInvocation,
					out var requiresContext);

				if (hasUltraLocal && !requiresContext)
				{
					InitializeDirectLocalContext(message, context);

					if (ultraLocalInvocation.IsCompletedSuccessfully)
					{
						TrySetContextResult(context, ultraLocalInvocation.Result);
						return DirectLocalSuccessResultTask;
					}

					return AwaitDirectLocalUntypedWithResponseAsync(ultraLocalInvocation, context);
				}

				if (requiresContext)
				{
					InitializeDirectLocalContext(message, context);
					if (localMessageBus.TryInvokeDirect(action, context, cancellationToken, out var invocation))
					{
						if (invocation.IsCompletedSuccessfully)
						{
							TrySetContextResult(context, invocation.Result);
							return DirectLocalSuccessResultTask;
						}

						return AwaitDirectLocalUntypedWithResponseAsync(invocation, context);
					}

					var sendTaskWithContext = localMessageBus.SendAsync(action, context, cancellationToken);
					if (sendTaskWithContext.IsCompletedSuccessfully)
					{
						return DirectLocalSuccessResultTask;
					}

					return AwaitDirectLocalSendUntypedWithResponseAsync(sendTaskWithContext, context);
				}

				InitializeDirectLocalContext(message, context);
				var sendTask = localMessageBus.SendAsync(action, context, cancellationToken);
				if (sendTask.IsCompletedSuccessfully)
				{
					return DirectLocalSuccessResultTask;
				}

				return AwaitDirectLocalSendUntypedWithResponseAsync(sendTask, context);
			}
			catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}
			catch (Exception ex)
			{
				return Task.FromResult(CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed"));
			}
		}
		finally
		{
			PopAmbientContext(previous);
		}
	}

	private Task<IMessageResult> DispatchDirectLocalEventAsync<TMessage>(
		TMessage message,
		IDispatchEvent evt,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		// PERF: Null checks removed — already validated by DispatchAsync caller.

		if (cancellationToken.IsCancellationRequested)
		{
			if (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}

			cancellationToken.ThrowIfCancellationRequested();
		}

		var previous = PushAmbientContext(context);
		try
		{
			try
			{
				InitializeDirectLocalContext(message, context);
				var invocation = localMessageBus!.PublishAsync(evt, context, cancellationToken);
				if (invocation.IsCompletedSuccessfully)
				{
					return DirectLocalSuccessResultTask;
				}

				return AwaitDirectLocalEventAsync(invocation, context);
			}
			catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
			{
				return CancelledResultTask;
			}
			catch (Exception ex)
			{
				return Task.FromResult(CreateDirectLocalFailureResult(ex, context, "Direct local event dispatch failed"));
			}
		}
		finally
		{
			PopAmbientContext(previous);
		}
	}

	private async Task<IMessageResult> AwaitDirectLocalNoResponseAsync(ValueTask invocation, IMessageContext context)
	{
		try
		{
			await invocation.ConfigureAwait(false);
			return SimpleMessageResult.SuccessResult;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult> AwaitDirectLocalSendNoResponseAsync(Task sendTask, IMessageContext context)
	{
		try
		{
			await sendTask.ConfigureAwait(false);
			return SimpleMessageResult.SuccessResult;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult<TResponse>> AwaitDirectLocalWithResponseAsync<TResponse>(
		ValueTask<object?> invocation,
		IMessageContext context)
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			return CreateDirectLocalTypedSuccessResult<TResponse>(result, context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return MessageResultOfT<TResponse>.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult<TResponse>(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult<TResponse>> AwaitDirectLocalTypedWithResponseAsync<TResponse>(
		ValueTask<TResponse?> invocation,
		IMessageContext context)
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			return CreateDirectLocalTypedSuccessResult(result, context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return MessageResultOfT<TResponse>.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult<TResponse>(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult<TResponse>> AwaitDirectLocalSendWithResponseAsync<TResponse>(
		Task sendTask,
		IMessageContext context)
	{
		try
		{
			await sendTask.ConfigureAwait(false);
			return CreateDirectLocalTypedSuccessResult<TResponse>(GetResultFromContext(context), context);
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return MessageResultOfT<TResponse>.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult<TResponse>(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult> AwaitDirectLocalUntypedWithResponseAsync(
		ValueTask<object?> invocation,
		IMessageContext context)
	{
		try
		{
			var result = await invocation.ConfigureAwait(false);
			TrySetContextResult(context, result);
			return SimpleMessageResult.SuccessResult;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult> AwaitDirectLocalSendUntypedWithResponseAsync(
		Task sendTask,
		IMessageContext context)
	{
		try
		{
			await sendTask.ConfigureAwait(false);
			return SimpleMessageResult.SuccessResult;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult(ex, context, "Direct local dispatch failed");
		}
	}

	private async Task<IMessageResult> AwaitDirectLocalEventAsync(Task invocation, IMessageContext context)
	{
		try
		{
			await invocation.ConfigureAwait(false);
			return SimpleMessageResult.SuccessResult;
		}
		catch (OperationCanceledException) when (ShouldReturnCancelledResult(context))
		{
			return Messaging.MessageResult.Cancelled();
		}
		catch (Exception ex)
		{
			return CreateDirectLocalFailureResult(ex, context, "Direct local event dispatch failed");
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void TrySetContextResult(IMessageContext context, object? result)
	{
		if (result is null)
		{
			// Preserve hot-path behavior for non-null results while ensuring reused contexts
			// do not leak stale values when a handler intentionally returns null.
			if (context.Result is not null)
			{
				context.Result = null;
			}

			return;
		}

		context.Result = result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private object? GetResultFromContext(IMessageContext context)
	{
		if (context.Result is not null)
		{
			return context.Result;
		}

		if (context is MessageContext messageContext)
		{
			return messageContext.TryGetItemFast(ResultContextKey, out var fastCachedValue)
				? fastCachedValue
				: null;
		}

		return context.GetItem<object?>(ResultContextKey);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Task<IMessageResult> GetCachedResultTask(IMessageResult result)
	{
		if (ReferenceEquals(result, SimpleMessageResult.SuccessResult))
		{
			return DirectLocalSuccessResultTask;
		}

		if (ReferenceEquals(result, SimpleMessageResult.SuccessCacheHitResult))
		{
			return DirectLocalSuccessCacheHitResultTask;
		}

		return Task.FromResult(result);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IMessageResult<TResponse> CreateDirectLocalTypedSuccessResult<TResponse>(TResponse? directResult, IMessageContext context)
	{
		if (ResponseTypeTraits<TResponse>.AlwaysHasValue || directResult is not null)
		{
			context.Result = directResult;
		}

		if (!_emitDirectLocalResultMetadata)
		{
			return new SimpleSuccessMessageResultOfT<TResponse>(directResult, cacheHit: false);
		}

		return new SimpleMessageResultOfT<TResponse>(
			value: directResult,
			succeeded: true,
			cacheHit: false,
			routingDecision: context.RoutingDecision,
			validationResult: context.ValidationResult(),
			authorizationResult: context.AuthorizationResult());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IMessageResult<TResponse> CreateDirectLocalTypedSuccessResult<TResponse>(object? directResult, IMessageContext context)
	{
		if (directResult is TResponse typed)
		{
			// Reuse the existing object box when possible (value types) to avoid a second boxing
			// via context.Result = typed in the generic overload.
			context.Result = directResult;

			if (!_emitDirectLocalResultMetadata)
			{
				return new SimpleSuccessMessageResultOfT<TResponse>(typed, cacheHit: false);
			}

			return new SimpleMessageResultOfT<TResponse>(
				value: typed,
				succeeded: true,
				cacheHit: false,
				routingDecision: context.RoutingDecision,
				validationResult: context.ValidationResult(),
				authorizationResult: context.AuthorizationResult());
		}

		if (directResult is null)
		{
			// Reused contexts may carry stale values from prior dispatches.
			// A null direct result must clear any prior context result before projection.
			if (context.Result is not null)
			{
				context.Result = null;
			}

			return CreateDirectLocalTypedSuccessResult<TResponse>(default, context);
		}

		return CreateDirectLocalTypedSuccessResult(ResolveResponseValue<TResponse>(context), context);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageResult CreateDirectLocalFailureResult(Exception ex, IMessageContext context, string title)
	{
		var problem = new MessageProblemDetails
		{
			Type = "dispatch.handler_error",
			Title = title,
			Status = 500,
			Detail = ex.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return Messaging.MessageResult.Failure(
			problem,
			context.RoutingDecision,
			context.ValidationResult() as IValidationResult,
			context.AuthorizationResult() as IAuthorizationResult);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageResult<TResponse> CreateDirectLocalFailureResult<TResponse>(
		Exception ex,
		IMessageContext context,
		string title)
	{
		var problem = new MessageProblemDetails
		{
			Type = "dispatch.handler_error",
			Title = title,
			Status = 500,
			Detail = ex.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return MessageResultOfT<TResponse>.Failure(
			problem,
			context.RoutingDecision,
			context.ValidationResult() as IValidationResult,
			context.AuthorizationResult() as IAuthorizationResult);
	}

	private static IMessageResult<TResponse> ConvertResult<TResponse>(IMessageResult result, IMessageContext context)
	{
		if (result is IMessageResult<TResponse> typedResult)
		{
			return typedResult;
		}

		// Common path: non-generic success singleton from final handler.
		if (ReferenceEquals(result, SimpleMessageResult.SuccessResult))
		{
			return new SimpleSuccessMessageResultOfT<TResponse>(ResolveResponseValue<TResponse>(context), cacheHit: false);
		}

		if (ReferenceEquals(result, SimpleMessageResult.SuccessCacheHitResult))
		{
			return new SimpleSuccessMessageResultOfT<TResponse>(ResolveResponseValue<TResponse>(context), cacheHit: true);
		}

		var value = ResolveResponseValue<TResponse>(context);
		if (result.Succeeded &&
		    result.ErrorMessage is null &&
		    result.ProblemDetails is null &&
		    result.ValidationResult is null &&
		    result.AuthorizationResult is null)
		{
			return new SimpleSuccessMessageResultOfT<TResponse>(value, cacheHit: result.CacheHit);
		}

		return new SimpleMessageResultOfT<TResponse>(
			value: value,
			succeeded: result.Succeeded,
			errorMessage: result.ErrorMessage,
			cacheHit: result.CacheHit,
			validationResult: result.ValidationResult,
			authorizationResult: result.AuthorizationResult,
			problemDetails: result.ProblemDetails);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TResponse? ResolveResponseValue<TResponse>(IMessageContext context)
	{
		if (context.Result is TResponse typedValue)
		{
			return typedValue;
		}

		if (context is MessageContext messageContext)
		{
			if (messageContext.TryGetItemFast(ResultContextKey, out var fastCachedValue))
			{
				return fastCachedValue is TResponse fastTypedCachedValue ? fastTypedCachedValue : default;
			}

			return default;
		}

		return context.GetItem<object?>(ResultContextKey) is TResponse cachedValue
			? cachedValue
			: default;
	}

	private static class CancelledResultTaskCache<TResponse>
	{
		internal static readonly Task<IMessageResult<TResponse>> Task =
			System.Threading.Tasks.Task.FromResult<IMessageResult<TResponse>>(MessageResultOfT<TResponse>.Cancelled());
	}

	private static class ResponseTypeTraits<TResponse>
	{
		internal static readonly bool AlwaysHasValue =
			typeof(TResponse).IsValueType && Nullable.GetUnderlyingType(typeof(TResponse)) is null;
	}

	private static bool IsDirectLocalActionPathEnabled(
		IDispatchMiddlewareInvoker? middlewareInvoker,
		IDictionary<string, IMessageBusOptions>? busOptionsMap)
	{
		if (middlewareInvoker is not DispatchMiddlewareInvoker)
		{
			return false;
		}

		return !(busOptionsMap?.TryGetValue(LocalBusName, out var localOptions) == true && localOptions?.EnableRetries == true);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CanBypassMiddlewareForMessage(IDispatchMessage message)
	{
		if (_canBypassAllMiddleware)
		{
			return true;
		}

		if (_concreteMiddlewareInvoker is null)
		{
			return false;
		}

		var messageType = message.GetType();
		return _middlewareBypassCache.GetOrAdd(
			messageType,
			static (type, invoker) => invoker.CanBypassFor(type),
			_concreteMiddlewareInvoker);
	}

	/// <summary>
	/// Gets combined dispatch info for a message type in a single cache lookup, replacing
	/// repeated type checks and middleware applicability queries with one
	/// <see cref="ConcurrentDictionary{TKey,TValue}"/> access.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private MessageDispatchInfo GetMessageDispatchInfo(Type messageType)
	{
		if (ReferenceEquals(s_cachedDispatchInfoDispatcher, this) &&
		    ReferenceEquals(s_cachedDispatchInfoType, messageType) &&
		    s_cachedDispatchInfoInitialized)
		{
			return s_cachedDispatchInfo;
		}

		var info = _messageDispatchInfoCache.GetOrAdd(
			messageType,
			static (type, state) =>
			{
				var canBypass = state._canBypassAllMiddleware ||
				                (state._concreteMiddlewareInvoker?.CanBypassFor(type) ?? false);

				var isAction = typeof(IDispatchAction).IsAssignableFrom(type);
				var isEvent = typeof(IDispatchEvent).IsAssignableFrom(type);
				var expectsResponse = false;
				var directLocalNoResponseEligible = false;
				var directLocalTypedEligible = false;
				Func<IDispatchAction, CancellationToken, ValueTask>? directLocalNoResponseInvoker = null;
				Func<IDispatchAction, CancellationToken, ValueTask<object?>>? directLocalWithResponseInvoker = null;

				if (isAction &&
				    state._directLocalActionPathEnabled &&
				    state.TryGetDirectLocalFastPath(
					    type,
					    out var directExpectsResponse,
					    out var directRequiresContext,
					    out var resolvedNoResponseInvoker,
					    out var resolvedWithResponseInvoker))
				{
					expectsResponse = directExpectsResponse;
					if (!directRequiresContext)
					{
						directLocalNoResponseInvoker = resolvedNoResponseInvoker;
						directLocalWithResponseInvoker = resolvedWithResponseInvoker;
						directLocalTypedEligible = directExpectsResponse && resolvedWithResponseInvoker is not null;
						directLocalNoResponseEligible = !directExpectsResponse && resolvedNoResponseInvoker is not null;
					}
				}
				else
				{
					foreach (var iface in type.GetInterfaces())
					{
						if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
						{
							expectsResponse = true;
							break;
						}
					}
				}

				return new MessageDispatchInfo(
					canBypass,
					expectsResponse,
					isAction,
					isEvent,
					directLocalNoResponseEligible,
					directLocalTypedEligible,
					directLocalNoResponseInvoker,
					directLocalWithResponseInvoker);
			},
			this);

		s_cachedDispatchInfoDispatcher = this;
		s_cachedDispatchInfoType = messageType;
		s_cachedDispatchInfo = info;
		s_cachedDispatchInfoInitialized = true;
		return info;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetDirectLocalFastPath(
		Type messageType,
		out bool expectsResponse,
		out bool requiresContext,
		out Func<IDispatchAction, CancellationToken, ValueTask>? noResponseInvoker,
		out Func<IDispatchAction, CancellationToken, ValueTask<object?>>? withResponseInvoker)
	{
		if (localMessageBus is null)
		{
			expectsResponse = false;
			requiresContext = false;
			noResponseInvoker = null;
			withResponseInvoker = null;
			return false;
		}

		return localMessageBus.TryGetUltraLocalFastInvokers(
			messageType,
			out expectsResponse,
			out requiresContext,
			out noResponseInvoker,
			out withResponseInvoker);
	}

	/// <summary>
	/// Cached per-type dispatch metadata computed once and reused for all subsequent dispatches.
	/// Eliminates multiple dictionary lookups and type checks on the hot path.
	/// </summary>
	private readonly struct MessageDispatchInfo(
		bool canBypassMiddleware,
		bool expectsResponse,
		bool isAction,
		bool isEvent,
		bool directLocalNoResponseEligible,
		bool directLocalTypedEligible,
		Func<IDispatchAction, CancellationToken, ValueTask>? directLocalNoResponseInvoker,
		Func<IDispatchAction, CancellationToken, ValueTask<object?>>? directLocalWithResponseInvoker)
	{
		/// <summary>Gets a value indicating whether middleware can be bypassed for this message type.</summary>
		public bool CanBypassMiddleware { get; } = canBypassMiddleware;

		/// <summary>Gets a value indicating whether this message type expects a typed response.</summary>
		public bool ExpectsResponse { get; } = expectsResponse;

		/// <summary>Gets a value indicating whether this message type implements <see cref="IDispatchAction"/>.</summary>
		public bool IsAction { get; } = isAction;

		/// <summary>Gets a value indicating whether this message type implements <see cref="IDispatchEvent"/>.</summary>
		public bool IsEvent { get; } = isEvent;

		/// <summary>
		/// Gets a value indicating whether the message type can use the pre-validated direct-local no-response path.
		/// </summary>
		public bool DirectLocalNoResponseEligible { get; } = directLocalNoResponseEligible;

		/// <summary>
		/// Gets a value indicating whether the message type can use the pre-validated direct-local typed path.
		/// </summary>
		public bool DirectLocalTypedEligible { get; } = directLocalTypedEligible;

		/// <summary>
		/// Gets the prevalidated no-response direct-local invoker.
		/// </summary>
		public Func<IDispatchAction, CancellationToken, ValueTask>? DirectLocalNoResponseInvoker { get; } =
			directLocalNoResponseInvoker;

		/// <summary>
		/// Gets the prevalidated with-response direct-local invoker.
		/// </summary>
		public Func<IDispatchAction, CancellationToken, ValueTask<object?>>? DirectLocalWithResponseInvoker { get; } =
			directLocalWithResponseInvoker;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsLocalRoute(IMessageContext context)
	{
		var endpoints = context.RoutingDecision?.Endpoints;
		if (endpoints is not { Count: > 0 })
		{
			return true;
		}

		return endpoints.Count == 1 && string.Equals(endpoints[0], LocalBusName, StringComparison.OrdinalIgnoreCase);
	}

	private static bool ShouldReturnCancelledResult(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (context is MessageContext messageContext)
		{
			if (messageContext.TryGetItemFast(ReturnCancelledResultContextKey, out var value))
			{
				return value is bool fastFlag
					? fastFlag
					: context.GetItem(ReturnCancelledResultContextKey, false);
			}

			return false;
		}

		return context.GetItem(ReturnCancelledResultContextKey, false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IMessageContext? PushAmbientContext(IMessageContext context)
	{
		if (!_ambientContextFlowEnabled)
		{
			return null;
		}

		var previous = MessageContextHolder.Current;
		MessageContextHolder.Current = context;
		return previous;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void PopAmbientContext(IMessageContext? previous)
	{
		if (_ambientContextFlowEnabled)
		{
			MessageContextHolder.Current = previous;
		}
	}

	/// <summary>
	/// Dispatches a document to a streaming handler and returns the output stream.
	/// </summary>
	/// <typeparam name="TDocument">Type of document being dispatched.</typeparam>
	/// <typeparam name="TOutput">Type of output items produced by the handler.</typeparam>
	/// <param name="document">The document to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>An asynchronous stream of output items.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the dispatcher is not configured or when no handler is registered.
	/// </exception>
	public async IAsyncEnumerable<TOutput> DispatchStreamingAsync<TDocument, TOutput>(
		TDocument document,
		IMessageContext context,
		[EnumeratorCancellation] CancellationToken cancellationToken)
		where TDocument : IDispatchDocument
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);

		if (serviceProvider == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		// Resolve the streaming handler from DI
		var handler = serviceProvider.GetService<IStreamingDocumentHandler<TDocument, TOutput>>()
		              ?? throw new InvalidOperationException(
			              string.Format(
				              CultureInfo.CurrentCulture,
				              Resources.Dispatcher_HandlerNotFoundFormat,
				              typeof(IStreamingDocumentHandler<TDocument, TOutput>).Name));

		// Set ambient context
		var previous = PushAmbientContext(context);

		try
		{
			// PERF-6: Use lazy generation for CorrelationId and CausationId.
			if (context is MessageContext streamMc)
			{
				streamMc.MarkForLazyCorrelation();
				streamMc.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				context.CausationId ??= context.CorrelationId;
			}

			// Use cached type name
			var documentType = document.GetType();
			context.MessageType = MessageTypeCache.GetTypeName(documentType);

			// Stream results from handler
			await foreach (var item in handler.HandleAsync(document, cancellationToken)
				               .WithCancellation(cancellationToken)
				               .ConfigureAwait(false))
			{
				yield return item;
			}
		}
		finally
		{
			// Restore previous context
			PopAmbientContext(previous);
		}
	}

	/// <summary>
	/// Dispatches a stream of documents to a consumer handler.
	/// </summary>
	/// <typeparam name="TDocument">Type of documents in the stream.</typeparam>
	/// <param name="documents">The stream of documents to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>A task that completes when the stream has been fully processed.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the dispatcher is not configured or when no handler is registered.
	/// </exception>
	public async Task DispatchStreamAsync<TDocument>(
		IAsyncEnumerable<TDocument> documents,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TDocument : IDispatchDocument
	{
		ArgumentNullException.ThrowIfNull(documents);
		ArgumentNullException.ThrowIfNull(context);

		if (serviceProvider == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		// Resolve the stream consumer handler from DI
		var handler = serviceProvider.GetService<IStreamConsumerHandler<TDocument>>()
		              ?? throw new InvalidOperationException(
			              string.Format(
				              CultureInfo.CurrentCulture,
				              Resources.Dispatcher_HandlerNotFoundFormat,
				              typeof(IStreamConsumerHandler<TDocument>).Name));

		// Set ambient context
		var previous = PushAmbientContext(context);

		try
		{
			// PERF-6: Use lazy generation for CorrelationId and CausationId.
			if (context is MessageContext streamMc)
			{
				streamMc.MarkForLazyCorrelation();
				streamMc.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				context.CausationId ??= context.CorrelationId;
			}

			// Delegate to handler - it controls consumption rate
			await handler.HandleAsync(documents, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Restore previous context
			PopAmbientContext(previous);
		}
	}

	/// <summary>
	/// Dispatches an input stream through a transform handler and returns the output stream.
	/// </summary>
	/// <typeparam name="TInput">Type of input documents in the stream.</typeparam>
	/// <typeparam name="TOutput">Type of output items produced by the transformation.</typeparam>
	/// <param name="input">The input stream of documents to transform.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>An asynchronous stream of transformed output items.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the dispatcher is not configured or when no handler is registered.
	/// </exception>
	public async IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
		IAsyncEnumerable<TInput> input,
		IMessageContext context,
		[EnumeratorCancellation] CancellationToken cancellationToken)
		where TInput : IDispatchDocument
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(context);

		if (serviceProvider == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		// Resolve the transform handler from DI
		var handler = serviceProvider.GetService<IStreamTransformHandler<TInput, TOutput>>()
		              ?? throw new InvalidOperationException(
			              string.Format(
				              CultureInfo.CurrentCulture,
				              Resources.Dispatcher_HandlerNotFoundFormat,
				              typeof(IStreamTransformHandler<TInput, TOutput>).Name));

		// Set ambient context
		var previous = PushAmbientContext(context);

		try
		{
			// PERF-6: Use lazy generation for CorrelationId and CausationId.
			if (context is MessageContext streamMc)
			{
				streamMc.MarkForLazyCorrelation();
				streamMc.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				context.CausationId ??= context.CorrelationId;
			}

			// Stream transformed results from handler
			await foreach (var item in handler.HandleAsync(input, cancellationToken)
				               .WithCancellation(cancellationToken)
				               .ConfigureAwait(false))
			{
				yield return item;
			}
		}
		finally
		{
			// Restore previous context
			PopAmbientContext(previous);
		}
	}

	/// <summary>
	/// Dispatches a document to a progress-reporting handler.
	/// </summary>
	/// <typeparam name="TDocument">Type of document being dispatched.</typeparam>
	/// <param name="document">The document to process.</param>
	/// <param name="context">Context for the dispatch operation.</param>
	/// <param name="progress">The progress reporter for status updates.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>A task that completes when the document has been fully processed.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the dispatcher is not configured or when no handler is registered.
	/// </exception>
	public async Task DispatchWithProgressAsync<TDocument>(
		TDocument document,
		IMessageContext context,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
		where TDocument : IDispatchDocument
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(progress);

		if (serviceProvider == null)
		{
			throw new InvalidOperationException(Resources.Dispatcher_NotConfigured);
		}

		// Resolve the progress handler from DI
		var handler = serviceProvider.GetService<IProgressDocumentHandler<TDocument>>()
		              ?? throw new InvalidOperationException(
			              string.Format(
				              CultureInfo.CurrentCulture,
				              Resources.Dispatcher_HandlerNotFoundFormat,
				              typeof(IProgressDocumentHandler<TDocument>).Name));

		// Set ambient context
		var previous = PushAmbientContext(context);

		try
		{
			// PERF-6: Use lazy generation for CorrelationId and CausationId.
			if (context is MessageContext streamMc)
			{
				streamMc.MarkForLazyCorrelation();
				streamMc.MarkForLazyCausation();
			}
			else
			{
				context.CorrelationId ??= Uuid7Extensions.GenerateGuid().ToString();
				context.CausationId ??= context.CorrelationId;
			}

			// Use cached type name
			var documentType = document.GetType();
			context.MessageType = MessageTypeCache.GetTypeName(documentType);

			// Delegate to handler with progress reporter
			await handler.HandleAsync(document, progress, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Restore previous context
			PopAmbientContext(previous);
		}
	}
}

// Simple implementation of IMessageResult
