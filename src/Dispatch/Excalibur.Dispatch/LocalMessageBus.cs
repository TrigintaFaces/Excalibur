// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides an in-memory message bus implementation for handling commands, events, and documents within the same process.
/// </summary>
/// <param name="provider"> Service provider for dependency resolution and handler instantiation. </param>
/// <param name="registry"> Registry containing information about registered message handlers. </param>
/// <param name="activator"> Service responsible for creating instances of message handlers. </param>
/// <param name="invoker"> Service responsible for invoking handler methods with appropriate parameters. </param>
/// <param name="logger"> Logger for capturing message bus operations and diagnostics. </param>
public sealed partial class LocalMessageBus(
	IServiceProvider provider,
	IHandlerRegistry registry,
	IHandlerActivator activator,
	IHandlerInvoker invoker,
	ILogger<LocalMessageBus> logger) : IMessageBus
{
	private const string ResultContextKey = "Dispatch:Result";
	private const string CacheHitContextKey = "Dispatch:CacheHit";

	private readonly FrozenDictionary<Type, HandlerRegistryEntry> _frozenHandlerEntryMap =
		InitializeFrozenHandlerEntryMap(registry);

	private readonly FrozenDictionary<Type, HandlerRegistryEntry[]> _frozenEventHandlersMap =
		InitializeFrozenEventHandlersMap(registry);

	private readonly FrozenDictionary<Type, EventDispatchPlan[]> _frozenEventDispatchPlanMap =
		InitializeFrozenEventDispatchPlanMap(registry);

	private readonly FrozenDictionary<Type, DirectActionDispatchPlan> _frozenDirectActionPlanMap =
		InitializeFrozenDirectActionPlanMap(registry);

	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry> _handlerEntryCache = new();

	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry[]> _eventHandlersCache =
		InitializeEventHandlersCache(registry);

	private readonly ConcurrentDictionary<Type, EventDispatchPlan[]?> _eventDispatchPlanCache = new();

	private readonly ConcurrentDictionary<Type, DirectActionDispatchPlan?> _directActionPlanCache =
		InitializeDirectActionPlanCache(registry);

	private readonly ConcurrentDictionary<Type, PrecompiledDirectActionDispatchPlan?> _precompiledDirectActionPlanCache = new();
	private readonly ConcurrentDictionary<Type, bool> _selfRegisteredHandlerCache = new();
	private readonly ConcurrentDictionary<Type, bool> _singletonNoContextEligibilityCache = new();
	private readonly ConcurrentDictionary<Type, object> _singletonNoContextHandlerCache = new();
	private readonly ConcurrentDictionary<Type, NoContextActivationPlan> _noContextActivationPlanCache = new();
	private readonly ConcurrentDictionary<Type, ContextActivationPlan> _contextActivationPlanCache = new();
	private readonly ConcurrentDictionary<Type, Func<object>> _noContextResolverCache = new();
	private readonly ConcurrentDictionary<Type, Func<IMessageContext, IServiceProvider, object>> _contextResolverCache = new();

	private readonly IServiceProviderIsService? _serviceProviderIsService =
		provider.GetService(typeof(IServiceProviderIsService)) as IServiceProviderIsService;

	private readonly IValueTaskHandlerInvoker? _valueTaskInvoker = invoker as IValueTaskHandlerInvoker;

	private delegate bool DirectActionNoResponseSyncInvoker(
		LocalMessageBus bus,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken,
		out ValueTask pendingInvocation);

	private delegate ValueTask DirectActionNoResponseAsyncInvoker(
		LocalMessageBus bus,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken);

	private delegate bool DirectActionWithResponseSyncInvoker(
		LocalMessageBus bus,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken,
		out object? result,
		out ValueTask<object?> pendingInvocation);

	private delegate ValueTask<object?> DirectActionWithResponseAsyncInvoker(
		LocalMessageBus bus,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken);

	private delegate ValueTask EventHandlerAsyncInvoker(
		LocalMessageBus bus,
		IDispatchEvent evt,
		IMessageContext? context,
		CancellationToken cancellationToken);

	private delegate bool PrecompiledDirectCanHandleDelegate(Type actionType);

	private delegate bool PrecompiledDirectTryGetMetadataDelegate(
		Type actionType,
		out bool expectsResponse,
		out bool requiresContext);

	private delegate ValueTask<object?> PrecompiledDirectInvokeDelegate(
		IDispatchAction action,
		IServiceProvider provider,
		IMessageContext? context,
		CancellationToken cancellationToken);

#if NET9_0_OR_GREATER
	private static readonly Lock PrecompiledDirectProviderLock = new();
#else
	private static readonly object PrecompiledDirectProviderLock = new();
#endif
	private static PrecompiledDirectProvider[] _precompiledDirectProviders = [];
	private static volatile bool _precompiledDirectProvidersInitialized;
	private static readonly IMessageContext NoContextActivationContext = new MessageContext();

	static LocalMessageBus()
	{
		AppDomain.CurrentDomain.AssemblyLoad += static (_, _) =>
		{
			lock (PrecompiledDirectProviderLock)
			{
				_precompiledDirectProvidersInitialized = false;
				_precompiledDirectProviders = [];
			}
		};
	}

	/// <summary>
	/// Sends a command or action message to its registered handler for processing.
	/// </summary>
	/// <param name="action"> The action/command message to send for processing. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous send operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="action" /> or <paramref name="context" /> is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when no handler is registered for the action type. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Handler types are discovered and preserved through source generation or explicit registration. The invoker uses cached delegates for AOT compatibility.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification =
			"The handler invocation is AOT-safe when handlers are registered through source generation or explicit registration with preserved types.")]
	public Task SendAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Check if we already have a cached result
		if (IsCacheHit(context) && HasContextResult(context))
		{
			// Result is already in context from cache, no need to execute handler
			return Task.CompletedTask;
		}

		var messageType = action.GetType();
		if (!TryGetHandlerEntry(messageType, out var entry))
		{
			throw new InvalidOperationException($"No handler registered for action {messageType.Name}");
		}

		var handler = ActivateHandler(entry.HandlerType, context);
		var invocation = InvokeHandler(handler, action, cancellationToken);
		if (!entry.ExpectsResponse)
		{
			return invocation.IsCompletedSuccessfully
				? Task.CompletedTask
				: AwaitNoResponseAsync(invocation);
		}

		if (invocation.IsCompletedSuccessfully)
		{
			var completedResult = invocation.Result;
			if (completedResult != null)
			{
				context.Result = completedResult;
			}

			return Task.CompletedTask;
		}

		return AwaitWithResponseAsync(invocation, context);
	}

	/// <summary>
	/// Publishes an event message to all registered handlers for parallel processing.
	/// </summary>
	/// <param name="evt"> The event message to publish to registered handlers. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="evt" /> or <paramref name="context" /> is null. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Handler types are discovered and preserved through source generation or explicit registration. The invoker uses cached delegates for AOT compatibility.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification =
			"The handler invocation is AOT-safe when handlers are registered through source generation or explicit registration with preserved types.")]
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = evt.GetType();
		var handlers = GetEventHandlers(messageType);

		if (handlers.Length == 0)
		{
			LogNoHandlersRegisteredForEvent(messageType.Name);
			return;
		}

		var plans = GetEventDispatchPlans(messageType, handlers);
		if (plans.Length == 0)
		{
			return;
		}

		if (plans.Length == 1)
		{
			var singlePlan = plans[0];
			var singleInvocation = singlePlan.Invoke(
				this,
				evt,
				singlePlan.RequiresContext ? context : null,
				cancellationToken);
			if (!singleInvocation.IsCompletedSuccessfully)
			{
				await singleInvocation.ConfigureAwait(false);
			}

			return;
		}

		var hasFirstPending = false;
		ValueTask firstPending = default;
		ValueTask[]? pendingInvocations = null;
		var pendingCount = 0;
		for (var i = 0; i < plans.Length; i++)
		{
			var plan = plans[i];
			var invocation = plan.Invoke(
				this,
				evt,
				plan.RequiresContext ? context : null,
				cancellationToken);
			if (invocation.IsCompletedSuccessfully)
			{
				continue;
			}

			if (!hasFirstPending)
			{
				firstPending = invocation;
				hasFirstPending = true;
				continue;
			}

			pendingInvocations ??= ArrayPool<ValueTask>.Shared.Rent(plans.Length - 1);
			pendingInvocations[pendingCount++] = invocation;
		}

		if (!hasFirstPending)
		{
			return;
		}

		try
		{
			await firstPending.ConfigureAwait(false);

			if (pendingInvocations is null || pendingCount == 0)
			{
				return;
			}

			for (var i = 0; i < pendingCount; i++)
			{
				await pendingInvocations[i].ConfigureAwait(false);
			}
		}
		finally
		{
			if (pendingInvocations is not null)
			{
				Array.Clear(pendingInvocations, 0, pendingCount);
				ArrayPool<ValueTask>.Shared.Return(pendingInvocations, clearArray: false);
			}
		}
	}

	/// <summary>
	/// Sends a document message to its registered handler for processing.
	/// </summary>
	/// <param name="doc"> The document message to send for processing. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous document sending operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="doc" /> or <paramref name="context" /> is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when no handler is registered for the document type. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Handler types are discovered and preserved through source generation or explicit registration. The invoker uses cached delegates for AOT compatibility.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification =
			"The handler invocation is AOT-safe when handlers are registered through source generation or explicit registration with preserved types.")]
	public Task SendDocumentAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = doc.GetType();
		if (!TryGetHandlerEntry(messageType, out var entry))
		{
			throw new InvalidOperationException($"No handler registered for document {messageType.Name}");
		}

		var handler = ActivateHandler(entry.HandlerType, context);
		var invocation = InvokeHandler(handler, doc, cancellationToken);
		return invocation.IsCompletedSuccessfully
			? Task.CompletedTask
			: AwaitNoResponseAsync(invocation);
	}

	/// <summary>
	/// Publishes an action message by delegating to the SendAsync method.
	/// </summary>
	/// <param name="action"> The action message to publish. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous publish operation. </returns>
	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
		=> SendAsync(action, context, cancellationToken);

	/// <summary>
	/// Publishes a document message by delegating to the SendDocumentAsync method.
	/// </summary>
	/// <param name="doc"> The document message to publish. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous publish operation. </returns>
	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
		=> SendDocumentAsync(doc, context, cancellationToken);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeDirect(
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		var actionType = action.GetType();
		if (!TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
			{
				if (TryGetCachedDirectResult(context, precompiledPlan.ExpectsResponse, out var precompiledCachedResult))
				{
					invocation = new ValueTask<object?>(precompiledCachedResult);
					return true;
				}

				invocation = precompiledPlan.Invoke(action, provider, context, cancellationToken);
				return true;
			}

			invocation = default;
			return false;
		}

		if (TryGetCachedDirectResult(context, resolvedPlan.ExpectsResponse, out var cachedResult))
		{
			invocation = new ValueTask<object?>(cachedResult);
			return true;
		}

		invocation = InvokePlan(resolvedPlan, action, context, cancellationToken);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeDirectNoResponse(
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken,
		out ValueTask invocation)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		var actionType = action.GetType();
		if (!TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
			{
				if (precompiledPlan.ExpectsResponse)
				{
					invocation = default;
					return false;
				}

				if (TryGetCachedDirectResult(context, expectsResponse: false, out _))
				{
					invocation = ValueTask.CompletedTask;
					return true;
				}

				var precompiledInvocation = precompiledPlan.Invoke(action, provider, context, cancellationToken);
				invocation = precompiledInvocation.IsCompletedSuccessfully
					? ValueTask.CompletedTask
					: AwaitNoResponseValueTaskAsync(precompiledInvocation);
				return true;
			}

			invocation = default;
			return false;
		}

		if (resolvedPlan.ExpectsResponse)
		{
			invocation = default;
			return false;
		}

		if (TryGetCachedDirectResult(context, expectsResponse: false, out _))
		{
			invocation = ValueTask.CompletedTask;
			return true;
		}

		if (resolvedPlan.TryInvokeNoResponseSync is { } syncInvoker)
		{
			_ = syncInvoker(this, action, context, cancellationToken, out invocation);
			return true;
		}

		if (resolvedPlan.InvokeNoResponseAsync is null)
		{
			invocation = default;
			return false;
		}

		invocation = resolvedPlan.InvokeNoResponseAsync(this, action, context, cancellationToken);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocal(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation)
	{
		return TryInvokeUltraLocal(action, cancellationToken, out invocation, out _);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalTyped<TMessage, TResponse>(
		TMessage action,
		CancellationToken cancellationToken,
		out ValueTask<TResponse?> invocation,
		out bool requiresContext)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(action);

		var actionType = action.GetType();
		if (!TryGetHandlerEntry(actionType, out var entry) || !entry.ExpectsResponse)
		{
			requiresContext = false;
			invocation = default;
			return false;
		}

		requiresContext = HandlerActivator.RequiresContextInjection(entry.HandlerType);
		if (requiresContext)
		{
			invocation = default;
			return false;
		}

		if (typeof(IActionHandler<TMessage, TResponse>).IsAssignableFrom(entry.HandlerType))
		{
			var handlerInstance = ResolveHandlerWithoutContext(entry.HandlerType);
			var task = ((IActionHandler<TMessage, TResponse>)handlerInstance).HandleAsync(action, cancellationToken);
			invocation = task.IsCompletedSuccessfully
				? new ValueTask<TResponse?>(GetCompletedTaskResult(task))
				: AwaitTypedResponseAsync(task);
			return true;
		}

		if (!TryInvokeUltraLocal(action, cancellationToken, out var fallbackInvocation, out requiresContext))
		{
			invocation = default;
			return false;
		}

		if (fallbackInvocation.IsCompletedSuccessfully)
		{
			invocation = new ValueTask<TResponse?>(CastTypedResponse<TResponse>(fallbackInvocation.Result));
			return true;
		}

		invocation = AwaitObjectAsTypedResponseAsync<TResponse>(fallbackInvocation);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocal(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation,
		out bool requiresContext)
	{
		ArgumentNullException.ThrowIfNull(action);

		var actionType = action.GetType();
		if (!TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
			{
				requiresContext = ResolveRequiresContext(actionType, precompiledPlan.RequiresContext);
				if (requiresContext)
				{
					invocation = default;
					return false;
				}

				invocation = precompiledPlan.Invoke(action, provider, context: null, cancellationToken);
				return true;
			}

			requiresContext = false;
			invocation = default;
			return false;
		}

		requiresContext = resolvedPlan.RequiresContext;
		if (resolvedPlan.RequiresContext)
		{
			invocation = default;
			return false;
		}

		invocation = InvokePlan(resolvedPlan, action, context: null, cancellationToken);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalNoResponse(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask invocation)
	{
		return TryInvokeUltraLocalNoResponse(action, cancellationToken, out invocation, out _);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalNoResponse(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask invocation,
		out bool requiresContext)
	{
		ArgumentNullException.ThrowIfNull(action);

		var actionType = action.GetType();
		if (!TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
			{
				if (precompiledPlan.ExpectsResponse)
				{
					requiresContext = false;
					invocation = default;
					return false;
				}

				requiresContext = ResolveRequiresContext(actionType, precompiledPlan.RequiresContext);
				if (requiresContext)
				{
					invocation = default;
					return false;
				}

				var precompiledInvocation = precompiledPlan.Invoke(action, provider, context: null, cancellationToken);
				invocation = precompiledInvocation.IsCompletedSuccessfully
					? ValueTask.CompletedTask
					: AwaitNoResponseValueTaskAsync(precompiledInvocation);
				return true;
			}

			requiresContext = false;
			invocation = default;
			return false;
		}

		if (resolvedPlan.ExpectsResponse)
		{
			requiresContext = false;
			invocation = default;
			return false;
		}

		requiresContext = resolvedPlan.RequiresContext;
		if (resolvedPlan.RequiresContext)
		{
			invocation = default;
			return false;
		}

		if (resolvedPlan.TryInvokeNoResponseSync is { } syncInvoker)
		{
			_ = syncInvoker(this, action, context: null, cancellationToken, out invocation);
			return true;
		}

		if (resolvedPlan.InvokeNoResponseAsync is null)
		{
			invocation = default;
			return false;
		}

		invocation = resolvedPlan.InvokeNoResponseAsync(this, action, context: null, cancellationToken);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetDirectActionDispatchMetadata(
		Type actionType,
		out bool expectsResponse,
		out bool requiresContext)
	{
		ArgumentNullException.ThrowIfNull(actionType);

		if (TryGetDirectActionDispatchPlan(actionType, out var runtimePlan))
		{
			expectsResponse = runtimePlan.ExpectsResponse;
			requiresContext = runtimePlan.RequiresContext;
			return true;
		}

		if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
		{
			expectsResponse = precompiledPlan.ExpectsResponse;
			requiresContext = ResolveRequiresContext(actionType, precompiledPlan.RequiresContext);
			return true;
		}

		expectsResponse = false;
		requiresContext = false;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ResolveRequiresContext(Type actionType, bool precompiledRequiresContext)
	{
		if (precompiledRequiresContext)
		{
			return true;
		}

		if (!TryGetHandlerEntry(actionType, out var entry))
		{
			return false;
		}

		return HandlerActivator.RequiresContextInjection(entry.HandlerType);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetDirectActionDispatchPlan(
		Type actionType,
		out DirectActionDispatchPlan resolvedPlan)
	{
		if (_frozenDirectActionPlanMap.TryGetValue(actionType, out var frozen))
		{
			resolvedPlan = frozen;
			return true;
		}

		if (!_directActionPlanCache.TryGetValue(actionType, out var plan))
		{
			plan = CreateDirectActionDispatchPlan(actionType);
			_ = _directActionPlanCache.TryAdd(actionType, plan);
		}

		if (plan is null)
		{
			resolvedPlan = default;
			return false;
		}

		resolvedPlan = plan.Value;
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask<object?> InvokePlan(
		in DirectActionDispatchPlan plan,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
	{
		if (plan.ExpectsResponse)
		{
			if (plan.TryInvokeWithResponseSync(this, action, context, cancellationToken, out var result, out var pending))
			{
				return new ValueTask<object?>(result);
			}

			return pending;
		}

		if (plan.TryInvokeNoResponseSync(this, action, context, cancellationToken, out var pendingNoResponse))
		{
			return new ValueTask<object?>(result: null);
		}

		return AwaitNoResponseAsObjectAsync(pendingNoResponse);
	}

	private HandlerRegistryEntry[] GetEventHandlers(Type messageType)
	{
		if (_frozenEventHandlersMap.TryGetValue(messageType, out var frozen))
		{
			return frozen;
		}

		if (_eventHandlersCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		if (registry is HandlerRegistry concreteRegistry &&
		    concreteRegistry.TryGetHandlerSnapshot(messageType, out var concreteEntries))
		{
			_ = _eventHandlersCache.TryAdd(messageType, concreteEntries);
			return concreteEntries;
		}

		var allHandlers = registry.GetAll();
		if (allHandlers.Count == 0)
		{
			return [];
		}

		var matchingHandlers = new List<HandlerRegistryEntry>();
		for (var i = 0; i < allHandlers.Count; i++)
		{
			var candidate = allHandlers[i];
			if (candidate.MessageType == messageType)
			{
				matchingHandlers.Add(candidate);
			}
		}

		if (matchingHandlers.Count == 0)
		{
			return [];
		}

		HandlerRegistryEntry[] resolvedHandlers = [.. matchingHandlers];
		_ = _eventHandlersCache.TryAdd(messageType, resolvedHandlers);
		return resolvedHandlers;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private EventDispatchPlan[] GetEventDispatchPlans(Type messageType, HandlerRegistryEntry[] handlers)
	{
		if (_frozenEventDispatchPlanMap.TryGetValue(messageType, out var frozen))
		{
			return frozen;
		}

		if (_eventDispatchPlanCache.TryGetValue(messageType, out var cached))
		{
			return cached ?? [];
		}

		var created = CreateEventDispatchPlans(handlers);
		_ = _eventDispatchPlanCache.TryAdd(messageType, created);
		return created;
	}

	private static EventDispatchPlan[] CreateEventDispatchPlans(HandlerRegistryEntry[] handlers)
	{
		if (handlers.Length == 0)
		{
			return [];
		}

		var plans = new EventDispatchPlan[handlers.Length];
		for (var index = 0; index < handlers.Length; index++)
		{
			var entry = handlers[index];
			var requiresContext = HandlerActivator.RequiresContextInjection(entry.HandlerType);
			EventHandlerAsyncInvoker invoker;
			if (TryCreateTypedEventAsyncInvoker(entry.MessageType, entry.HandlerType, out var typedInvoker))
			{
				invoker = typedInvoker;
			}
			else
			{
				// Unknown/legacy handler shapes stay on the context-aware fallback path.
				requiresContext = true;
				invoker = CreateEventAsyncInvoker(entry.HandlerType);
			}

			plans[index] = new EventDispatchPlan(requiresContext, invoker);
		}

		return plans;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private DirectActionDispatchPlan? CreateDirectActionDispatchPlan(Type actionType)
	{
		if (!TryGetHandlerEntry(actionType, out var entry))
		{
			return null;
		}

		return CreateRuntimeDirectActionDispatchPlan(entry);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionDispatchPlan CreateRuntimeDirectActionDispatchPlan(HandlerRegistryEntry entry)
	{
		if (TryCreateTypedDirectActionDispatchPlan(entry, out var typedPlan))
		{
			return typedPlan;
		}

		return new DirectActionDispatchPlan(
			entry.HandlerType,
			entry.ExpectsResponse,
			RequiresContext: HandlerActivator.RequiresContextInjection(entry.HandlerType),
			TryInvokeNoResponseSync: entry.ExpectsResponse ? null : CreateDirectActionNoResponseSyncInvoker(entry.HandlerType),
			InvokeNoResponseAsync: entry.ExpectsResponse ? null : CreateDirectActionNoResponseAsyncInvoker(entry.HandlerType),
			TryInvokeWithResponseSync: entry.ExpectsResponse ? CreateDirectActionWithResponseSyncInvoker(entry.HandlerType) : null,
			InvokeWithResponseAsync: entry.ExpectsResponse ? CreateDirectActionWithResponseAsyncInvoker(entry.HandlerType) : null);
	}

	private static bool TryCreateTypedDirectActionDispatchPlan(
		HandlerRegistryEntry entry,
		out DirectActionDispatchPlan plan)
	{
		if (!entry.ExpectsResponse)
		{
			if (!TryCreateTypedNoResponseAsyncInvoker(entry.MessageType, entry.HandlerType, out var invokeNoResponseAsync))
			{
				plan = default;
				return false;
			}

			plan = new DirectActionDispatchPlan(
				HandlerType: entry.HandlerType,
				ExpectsResponse: false,
				RequiresContext: HandlerActivator.RequiresContextInjection(entry.HandlerType),
				TryInvokeNoResponseSync: CreateDirectActionNoResponseSyncInvoker(invokeNoResponseAsync),
				InvokeNoResponseAsync: invokeNoResponseAsync,
				TryInvokeWithResponseSync: null,
				InvokeWithResponseAsync: null);
			return true;
		}

		if (!TryGetActionResponseType(entry.MessageType, out var responseType) ||
		    !TryCreateTypedWithResponseAsyncInvoker(entry.MessageType, entry.HandlerType, responseType, out var invokeWithResponseAsync))
		{
			plan = default;
			return false;
		}

		plan = new DirectActionDispatchPlan(
			HandlerType: entry.HandlerType,
			ExpectsResponse: true,
			RequiresContext: HandlerActivator.RequiresContextInjection(entry.HandlerType),
			TryInvokeNoResponseSync: null,
			InvokeNoResponseAsync: null,
			TryInvokeWithResponseSync: CreateDirectActionWithResponseSyncInvoker(invokeWithResponseAsync),
			InvokeWithResponseAsync: invokeWithResponseAsync);
		return true;
	}

	private static bool TryGetActionResponseType(Type actionType, out Type responseType)
	{
		foreach (var candidate in actionType.GetInterfaces())
		{
			if (candidate.IsGenericType &&
			    candidate.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
			{
				responseType = candidate.GetGenericArguments()[0];
				return true;
			}
		}

		responseType = null!;
		return false;
	}

	private static bool TryCreateTypedNoResponseAsyncInvoker(
		Type actionType,
		Type handlerType,
		out DirectActionNoResponseAsyncInvoker invoker)
	{
		if (!typeof(IDispatchAction).IsAssignableFrom(actionType) ||
		    !typeof(IActionHandler<>).MakeGenericType(actionType).IsAssignableFrom(handlerType))
		{
			invoker = null!;
			return false;
		}

		try
		{
			var method = typeof(LocalMessageBus).GetMethod(
				nameof(CreateTypedNoResponseAsyncInvokerCore),
				BindingFlags.NonPublic | BindingFlags.Static)!;
			var closed = method.MakeGenericMethod(actionType, handlerType);
			invoker = (DirectActionNoResponseAsyncInvoker)closed.Invoke(obj: null, parameters: null)!;
			return true;
		}
		catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
		{
			invoker = null!;
			return false;
		}
	}

	private static bool TryCreateTypedWithResponseAsyncInvoker(
		Type actionType,
		Type handlerType,
		Type responseType,
		out DirectActionWithResponseAsyncInvoker invoker)
	{
		var actionInterface = typeof(IDispatchAction<>).MakeGenericType(responseType);
		var handlerInterface = typeof(IActionHandler<,>).MakeGenericType(actionType, responseType);
		if (!actionInterface.IsAssignableFrom(actionType) || !handlerInterface.IsAssignableFrom(handlerType))
		{
			invoker = null!;
			return false;
		}

		try
		{
			var method = typeof(LocalMessageBus).GetMethod(
				nameof(CreateTypedWithResponseAsyncInvokerCore),
				BindingFlags.NonPublic | BindingFlags.Static)!;
			var closed = method.MakeGenericMethod(actionType, handlerType, responseType);
			invoker = (DirectActionWithResponseAsyncInvoker)closed.Invoke(obj: null, parameters: null)!;
			return true;
		}
		catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
		{
			invoker = null!;
			return false;
		}
	}

	private static bool TryCreateTypedEventAsyncInvoker(
		Type eventType,
		Type handlerType,
		out EventHandlerAsyncInvoker invoker)
	{
		if (!typeof(IDispatchEvent).IsAssignableFrom(eventType) ||
		    !typeof(IEventHandler<>).MakeGenericType(eventType).IsAssignableFrom(handlerType))
		{
			invoker = null!;
			return false;
		}

		try
		{
			var method = typeof(LocalMessageBus).GetMethod(
				nameof(CreateTypedEventAsyncInvokerCore),
				BindingFlags.NonPublic | BindingFlags.Static)!;
			var closed = method.MakeGenericMethod(eventType, handlerType);
			invoker = (EventHandlerAsyncInvoker)closed.Invoke(obj: null, parameters: null)!;
			return true;
		}
		catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
		{
			invoker = null!;
			return false;
		}
	}

	private static DirectActionNoResponseAsyncInvoker CreateTypedNoResponseAsyncInvokerCore<TAction, THandler>()
		where TAction : IDispatchAction
		where THandler : IActionHandler<TAction>
	{
		return static (bus, action, context, cancellationToken) =>
			bus.InvokeTypedNoResponse<TAction, THandler>(action, context, cancellationToken);
	}

	private static DirectActionWithResponseAsyncInvoker CreateTypedWithResponseAsyncInvokerCore<TAction, THandler, TResponse>()
		where TAction : IDispatchAction<TResponse>
		where THandler : IActionHandler<TAction, TResponse>
	{
		return static (bus, action, context, cancellationToken) =>
			bus.InvokeTypedWithResponse<TAction, THandler, TResponse>(action, context, cancellationToken);
	}

	private static EventHandlerAsyncInvoker CreateTypedEventAsyncInvokerCore<TEvent, THandler>()
		where TEvent : IDispatchEvent
		where THandler : IEventHandler<TEvent>
	{
		return static (bus, evt, context, cancellationToken) =>
			bus.InvokeTypedEvent<TEvent, THandler>(evt, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetPrecompiledDirectActionDispatchPlan(
		Type actionType,
		out PrecompiledDirectActionDispatchPlan plan)
	{
		if (_precompiledDirectActionPlanCache.TryGetValue(actionType, out var cached))
		{
			if (cached is null)
			{
				plan = default;
				return false;
			}

			plan = cached.Value;
			return true;
		}

		var resolved = ResolvePrecompiledDirectActionDispatchPlan(actionType);
		_ = _precompiledDirectActionPlanCache.TryAdd(actionType, resolved);
		if (resolved is null)
		{
			plan = default;
			return false;
		}

		plan = resolved.Value;
		return true;
	}

	private static PrecompiledDirectActionDispatchPlan? ResolvePrecompiledDirectActionDispatchPlan(Type actionType)
	{
		var providers = GetPrecompiledDirectProviders();
		for (var index = 0; index < providers.Length; index++)
		{
			var provider = providers[index];
			try
			{
				if (!provider.CanHandle(actionType))
				{
					continue;
				}

				if (!provider.TryGetMetadata(actionType, out var expectsResponse, out var requiresContext))
				{
					continue;
				}

				return new PrecompiledDirectActionDispatchPlan(
					expectsResponse,
					requiresContext,
					provider.Invoke);
			}
			catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
			{
				// Ignore broken generated providers and continue probing.
			}
		}

		return null;
	}

	private static PrecompiledDirectProvider[] GetPrecompiledDirectProviders()
	{
		if (_precompiledDirectProvidersInitialized)
		{
			return _precompiledDirectProviders;
		}

		lock (PrecompiledDirectProviderLock)
		{
			if (_precompiledDirectProvidersInitialized)
			{
				return _precompiledDirectProviders;
			}

			var providers = new List<PrecompiledDirectProvider>();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (var index = 0; index < assemblies.Length; index++)
			{
				TryAddPrecompiledDirectProvider(assemblies[index], providers);
			}

			_precompiledDirectProviders = [.. providers];
			_precompiledDirectProvidersInitialized = true;
			return _precompiledDirectProviders;
		}
	}

	private static void TryAddPrecompiledDirectProvider(Assembly assembly, ICollection<PrecompiledDirectProvider> providers)
	{
		const string typeName = "Excalibur.Dispatch.Delivery.Handlers.PrecompiledDirectActionDispatch";

		Type? dispatchType;
		try
		{
			dispatchType = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
		{
			return;
		}

		if (dispatchType is null)
		{
			return;
		}

		var canHandleMethod = dispatchType.GetMethod(
			"CanHandle",
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			[typeof(Type)],
			modifiers: null);
		var tryGetMetadataMethod = dispatchType.GetMethod(
			"TryGetMetadata",
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			[typeof(Type), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType()],
			modifiers: null);
		var invokeMethod = dispatchType.GetMethod(
			"InvokeAsync",
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			[typeof(IDispatchAction), typeof(IServiceProvider), typeof(IMessageContext), typeof(CancellationToken)],
			modifiers: null);

		if (canHandleMethod is null || tryGetMetadataMethod is null || invokeMethod is null)
		{
			return;
		}

		try
		{
			var canHandle = canHandleMethod.CreateDelegate<PrecompiledDirectCanHandleDelegate>();
			var tryGetMetadata = tryGetMetadataMethod.CreateDelegate<PrecompiledDirectTryGetMetadataDelegate>();
			var invoke = invokeMethod.CreateDelegate<PrecompiledDirectInvokeDelegate>();
			providers.Add(new PrecompiledDirectProvider(canHandle, tryGetMetadata, invoke));
		}
		catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
		{
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionNoResponseSyncInvoker CreateDirectActionNoResponseSyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var asyncInvoker = CreateDirectActionNoResponseAsyncInvoker(handlerType);
		return CreateDirectActionNoResponseSyncInvoker(asyncInvoker);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionNoResponseSyncInvoker CreateDirectActionNoResponseSyncInvoker(
		DirectActionNoResponseAsyncInvoker asyncInvoker)
	{
		return (
			LocalMessageBus bus,
			IDispatchAction action,
			IMessageContext? context,
			CancellationToken cancellationToken,
			out ValueTask pendingInvocation) =>
		{
			var invocation = asyncInvoker(bus, action, context, cancellationToken);
			if (invocation.IsCompletedSuccessfully)
			{
				pendingInvocation = default;
				return true;
			}

			pendingInvocation = invocation;
			return false;
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionNoResponseAsyncInvoker CreateDirectActionNoResponseAsyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		return (bus, action, context, cancellationToken) =>
			bus.InvokeDirectActionNoResponse(handlerType, action, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionWithResponseSyncInvoker CreateDirectActionWithResponseSyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var asyncInvoker = CreateDirectActionWithResponseAsyncInvoker(handlerType);
		return CreateDirectActionWithResponseSyncInvoker(asyncInvoker);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionWithResponseSyncInvoker CreateDirectActionWithResponseSyncInvoker(
		DirectActionWithResponseAsyncInvoker asyncInvoker)
	{
		return (
			LocalMessageBus bus,
			IDispatchAction action,
			IMessageContext? context,
			CancellationToken cancellationToken,
			out object? result,
			out ValueTask<object?> pendingInvocation) =>
		{
			var invocation = asyncInvoker(bus, action, context, cancellationToken);
			if (invocation.IsCompletedSuccessfully)
			{
				result = invocation.Result;
				pendingInvocation = default;
				return true;
			}

			result = null;
			pendingInvocation = invocation;
			return false;
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionWithResponseAsyncInvoker CreateDirectActionWithResponseAsyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		return (bus, action, context, cancellationToken) =>
			bus.InvokeDirectAction(handlerType, action, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static EventHandlerAsyncInvoker CreateEventAsyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		return (bus, evt, context, cancellationToken) =>
			bus.InvokeEventHandler(handlerType, evt, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask InvokeDirectActionNoResponse(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
	{
		var invocation = InvokeDirectAction(handlerType, action, context, cancellationToken);
		return invocation.IsCompletedSuccessfully
			? ValueTask.CompletedTask
			: AwaitNoResponseValueTaskAsync(invocation);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask<object?> InvokeDirectAction(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
	{
		var handler = context is null
			? ResolveHandlerWithoutContext(handlerType)
			: ActivateHandler(handlerType, context);
		return InvokeHandler(handler, action, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask InvokeEventHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IDispatchEvent evt,
		IMessageContext? context,
		CancellationToken cancellationToken)
	{
		var handler = context is null
			? ResolveHandlerWithoutContext(handlerType)
			: ActivateHandler(handlerType, context);
		var invocation = InvokeHandler(handler, evt, cancellationToken);
		return invocation.IsCompletedSuccessfully
			? ValueTask.CompletedTask
			: AwaitNoResponseValueTaskAsync(invocation);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask InvokeTypedNoResponse<
		TAction,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		THandler>(
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
		where TAction : IDispatchAction
		where THandler : IActionHandler<TAction>
	{
		var handlerInstance = context is null
			? ResolveHandlerWithoutContext(typeof(THandler))
			: ActivateHandler(typeof(THandler), context);
		var task = ((THandler)handlerInstance).HandleAsync((TAction)action, cancellationToken);
		return task.IsCompletedSuccessfully
			? ValueTask.CompletedTask
			: new ValueTask(task);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask<object?> InvokeTypedWithResponse<
		TAction,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		THandler,
		TResponse>(
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
		where TAction : IDispatchAction<TResponse>
		where THandler : IActionHandler<TAction, TResponse>
	{
		var handlerInstance = context is null
			? ResolveHandlerWithoutContext(typeof(THandler))
			: ActivateHandler(typeof(THandler), context);
		var task = ((THandler)handlerInstance).HandleAsync((TAction)action, cancellationToken);
		return task.IsCompletedSuccessfully
			? new ValueTask<object?>(GetCompletedTaskResult(task))
			: AwaitTypedResponseAsObjectAsync(task);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask InvokeTypedEvent<
		TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		THandler>(
		IDispatchEvent evt,
		IMessageContext? context,
		CancellationToken cancellationToken)
		where TEvent : IDispatchEvent
		where THandler : IEventHandler<TEvent>
	{
		var handlerInstance = context is null
			? ResolveHandlerWithoutContext(typeof(THandler))
			: ActivateHandler(typeof(THandler), context);
		var task = ((THandler)handlerInstance).HandleAsync((TEvent)evt, cancellationToken);
		return task.IsCompletedSuccessfully
			? ValueTask.CompletedTask
			: new ValueTask(task);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private object ResolveHandlerWithoutContext(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (!_noContextResolverCache.TryGetValue(handlerType, out var resolver))
		{
			resolver = BuildNoContextResolver(handlerType);
			_ = _noContextResolverCache.TryAdd(handlerType, resolver);
		}

		return resolver();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetCachedDirectResult(IMessageContext context, bool expectsResponse, out object? result)
	{
		if (!IsCacheHit(context))
		{
			result = null;
			return false;
		}

		result = context.Result ?? context.GetItem<object?>(ResultContextKey);
		if (expectsResponse)
		{
			return result is not null;
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsCacheHit(IMessageContext context)
	{
		if (context is MessageContext messageContext &&
		    messageContext.TryGetItemFast(CacheHitContextKey, out var fastValue) &&
		    fastValue is bool fastFlag)
		{
			return fastFlag;
		}

		return context.GetItem(CacheHitContextKey, false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool HasContextResult(IMessageContext context)
	{
		if (context.Result is not null)
		{
			return true;
		}

		if (context is MessageContext messageContext &&
		    messageContext.TryGetItemFast(ResultContextKey, out var fastValue))
		{
			return fastValue is not null;
		}

		return context.GetItem<object?>(ResultContextKey) is not null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IServiceProvider GetProvider() => provider;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		IMessageContext context)
	{
		var requestProvider = context.RequestServices;
		var activationProvider = requestProvider ?? provider;
		if (!_contextResolverCache.TryGetValue(handlerType, out var resolver))
		{
			resolver = BuildContextResolver(handlerType);
			_ = _contextResolverCache.TryAdd(handlerType, resolver);
		}

		try
		{
			return resolver(context, activationProvider);
		}
		catch (InvalidOperationException ex) when (
			requestProvider is not null &&
			!ReferenceEquals(requestProvider, provider) &&
			LooksLikeMissingServiceResolution(ex))
		{
			return resolver(context, provider);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool LooksLikeMissingServiceResolution(InvalidOperationException exception)
	{
		var message = exception.Message;
		return message.Contains("No service for type", StringComparison.Ordinal) ||
		       message.Contains("Unable to resolve service for type", StringComparison.Ordinal);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Func<object> BuildNoContextResolver(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var activationPlan = GetNoContextActivationPlan(handlerType);
		return activationPlan.Mode switch
		{
			NoContextActivationMode.SingletonCached => () => activationPlan.SingletonHandler!,
			NoContextActivationMode.SelfRegistered => () => provider.GetRequiredService(handlerType),
			NoContextActivationMode.FactoryActivator => () => ((HandlerActivator)activator)
				.ActivateFactoryHandler(handlerType, NoContextActivationContext, provider),
			_ => () => activator.ActivateHandler(handlerType, NoContextActivationContext, provider),
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Func<IMessageContext, IServiceProvider, object> BuildContextResolver(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		var activationPlan = GetContextActivationPlan(handlerType);
		return activationPlan.Mode switch
		{
			ContextActivationMode.SingletonCached => (_, _) => activationPlan.SingletonHandler!,
			ContextActivationMode.RegisteredOptimized => (messageContext, activationProvider) => ((HandlerActivator)activator)
				.ActivateRegisteredHandler(handlerType, messageContext, activationProvider),
			ContextActivationMode.FactoryOptimized => (messageContext, activationProvider) => ((HandlerActivator)activator)
				.ActivateFactoryHandler(handlerType, messageContext, activationProvider),
			_ => (messageContext, activationProvider) => activator.ActivateHandler(handlerType, messageContext, activationProvider),
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private NoContextActivationPlan GetNoContextActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_noContextActivationPlanCache.TryGetValue(handlerType, out var cached))
		{
			return cached;
		}

		var created = BuildNoContextActivationPlan(handlerType);
		_ = _noContextActivationPlanCache.TryAdd(handlerType, created);
		return created;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ContextActivationPlan GetContextActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_contextActivationPlanCache.TryGetValue(handlerType, out var cached))
		{
			return cached;
		}

		var created = BuildContextActivationPlan(handlerType);
		_ = _contextActivationPlanCache.TryAdd(handlerType, created);
		return created;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private NoContextActivationPlan BuildNoContextActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (TryGetSingletonNoContextHandler(handlerType, out var singleton))
		{
			return new NoContextActivationPlan(NoContextActivationMode.SingletonCached, singleton);
		}

		if (IsSelfRegisteredHandler(handlerType))
		{
			return new NoContextActivationPlan(NoContextActivationMode.SelfRegistered, SingletonHandler: null);
		}

		return activator is HandlerActivator
			? new NoContextActivationPlan(NoContextActivationMode.FactoryActivator, SingletonHandler: null)
			: new NoContextActivationPlan(NoContextActivationMode.GenericActivator, SingletonHandler: null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ContextActivationPlan BuildContextActivationPlan(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (TryGetSingletonNoContextHandler(handlerType, out var singleton))
		{
			return new ContextActivationPlan(ContextActivationMode.SingletonCached, singleton);
		}

		if (activator is HandlerActivator)
		{
			return IsSelfRegisteredHandler(handlerType)
				? new ContextActivationPlan(ContextActivationMode.RegisteredOptimized, SingletonHandler: null)
				: new ContextActivationPlan(ContextActivationMode.FactoryOptimized, SingletonHandler: null);
		}

		return new ContextActivationPlan(ContextActivationMode.GenericActivator, SingletonHandler: null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsSelfRegisteredHandler(Type handlerType)
	{
		if (_serviceProviderIsService is null)
		{
			return false;
		}

		return _selfRegisteredHandlerCache.GetOrAdd(handlerType, static (type, isService) => isService.IsService(type),
			_serviceProviderIsService);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CanUseSingletonNoContextBypass(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		if (_singletonNoContextEligibilityCache.TryGetValue(handlerType, out var cached))
		{
			return cached;
		}

		var eligible = ComputeSingletonNoContextEligibility(handlerType);
		_ = _singletonNoContextEligibilityCache.TryAdd(handlerType, eligible);
		return eligible;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ComputeSingletonNoContextEligibility(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType)
	{
		return IsSelfRegisteredHandler(handlerType) && !HandlerActivator.RequiresContextInjection(handlerType);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetSingletonNoContextHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type handlerType,
		out object handler)
	{
		if (_singletonNoContextHandlerCache.TryGetValue(handlerType, out handler!))
		{
			return true;
		}

		if (!CanUseSingletonNoContextBypass(handlerType))
		{
			handler = default!;
			return false;
		}

		try
		{
			var first = provider.GetRequiredService(handlerType);
			var second = provider.GetRequiredService(handlerType);
			if (!ReferenceEquals(first, second))
			{
				_ = _singletonNoContextEligibilityCache.TryUpdate(handlerType, false, true);
				handler = default!;
				return false;
			}

			_ = _singletonNoContextHandlerCache.TryAdd(handlerType, first);
			handler = first;
			return true;
		}
		catch (InvalidOperationException)
		{
			_ = _singletonNoContextEligibilityCache.TryUpdate(handlerType, false, true);
			handler = default!;
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetHandlerEntry(Type messageType, out HandlerRegistryEntry entry)
	{
		if (_frozenHandlerEntryMap.TryGetValue(messageType, out var frozenEntry))
		{
			entry = frozenEntry;
			return true;
		}

		if (_handlerEntryCache.TryGetValue(messageType, out var cachedEntry))
		{
			entry = cachedEntry;
			return true;
		}

		if (!registry.TryGetHandler(messageType, out var resolvedEntry))
		{
			entry = default!;
			return false;
		}

		entry = resolvedEntry;
		_ = _handlerEntryCache.TryAdd(messageType, resolvedEntry);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask<object?> InvokeHandler(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		if (_valueTaskInvoker is not null)
		{
			return _valueTaskInvoker.InvokeValueTaskAsync(handler, message, cancellationToken);
		}

		return new ValueTask<object?>(invoker.InvokeAsync(handler, message, cancellationToken));
	}

	private static FrozenDictionary<Type, HandlerRegistryEntry> InitializeFrozenHandlerEntryMap(IHandlerRegistry registry)
	{
		var entries = registry.GetAll();
		if (entries.Count == 0)
		{
			return FrozenDictionary<Type, HandlerRegistryEntry>.Empty;
		}

		var map = new Dictionary<Type, HandlerRegistryEntry>(entries.Count);
		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];
			if (typeof(IDispatchEvent).IsAssignableFrom(entry.MessageType))
			{
				continue;
			}

			// Keep first registration for deterministic action/document dispatch lookup.
			map.TryAdd(entry.MessageType, entry);
		}

		return map.Count == 0
			? FrozenDictionary<Type, HandlerRegistryEntry>.Empty
			: map.ToFrozenDictionary();
	}

	private static FrozenDictionary<Type, HandlerRegistryEntry[]> InitializeFrozenEventHandlersMap(IHandlerRegistry registry)
	{
		var entries = registry.GetAll();
		if (entries.Count == 0)
		{
			return FrozenDictionary<Type, HandlerRegistryEntry[]>.Empty;
		}

		var grouped = new Dictionary<Type, List<HandlerRegistryEntry>>();
		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];
			if (!typeof(IDispatchEvent).IsAssignableFrom(entry.MessageType))
			{
				continue;
			}

			if (!grouped.TryGetValue(entry.MessageType, out var handlers))
			{
				handlers = new List<HandlerRegistryEntry>();
				grouped.Add(entry.MessageType, handlers);
			}

			handlers.Add(entry);
		}

		if (grouped.Count == 0)
		{
			return FrozenDictionary<Type, HandlerRegistryEntry[]>.Empty;
		}

		var resolved = new Dictionary<Type, HandlerRegistryEntry[]>(grouped.Count);
		foreach (var pair in grouped)
		{
			resolved[pair.Key] = [.. pair.Value];
		}

		return resolved.ToFrozenDictionary();
	}

	private static FrozenDictionary<Type, EventDispatchPlan[]> InitializeFrozenEventDispatchPlanMap(IHandlerRegistry registry)
	{
		var eventHandlers = InitializeFrozenEventHandlersMap(registry);
		if (eventHandlers.Count == 0)
		{
			return FrozenDictionary<Type, EventDispatchPlan[]>.Empty;
		}

		var plans = new Dictionary<Type, EventDispatchPlan[]>(eventHandlers.Count);
		foreach (var pair in eventHandlers)
		{
			plans[pair.Key] = CreateEventDispatchPlans(pair.Value);
		}

		return plans.ToFrozenDictionary();
	}

	private static FrozenDictionary<Type, DirectActionDispatchPlan> InitializeFrozenDirectActionPlanMap(IHandlerRegistry registry)
	{
		var entries = registry.GetAll();
		if (entries.Count == 0)
		{
			return FrozenDictionary<Type, DirectActionDispatchPlan>.Empty;
		}

		var plans = new Dictionary<Type, DirectActionDispatchPlan>(entries.Count);
		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];
			if (!typeof(IDispatchAction).IsAssignableFrom(entry.MessageType))
			{
				continue;
			}

			if (plans.ContainsKey(entry.MessageType))
			{
				continue;
			}

			plans.Add(entry.MessageType, CreateRuntimeDirectActionDispatchPlan(entry));
		}

		return plans.Count == 0
			? FrozenDictionary<Type, DirectActionDispatchPlan>.Empty
			: plans.ToFrozenDictionary();
	}

	private static ConcurrentDictionary<Type, HandlerRegistryEntry[]> InitializeEventHandlersCache(IHandlerRegistry registry)
	{
		if (registry is HandlerRegistry concreteRegistry)
		{
			concreteRegistry.PrecomputeSnapshots();
		}

		return new ConcurrentDictionary<Type, HandlerRegistryEntry[]>();
	}

	private static ConcurrentDictionary<Type, DirectActionDispatchPlan?> InitializeDirectActionPlanCache(IHandlerRegistry registry)
	{
		var cache = new ConcurrentDictionary<Type, DirectActionDispatchPlan?>();
		var entries = registry.GetAll();
		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];
			if (!typeof(IDispatchAction).IsAssignableFrom(entry.MessageType))
			{
				continue;
			}

			if (cache.ContainsKey(entry.MessageType))
			{
				continue;
			}

			_ = cache.TryAdd(entry.MessageType, CreateRuntimeDirectActionDispatchPlan(entry));
		}

		return cache;
	}

	private static async Task AwaitNoResponseAsync(ValueTask<object?> invocation)
	{
		_ = await invocation.ConfigureAwait(false);
	}

	private static async ValueTask AwaitNoResponseValueTaskAsync(ValueTask<object?> invocation)
	{
		_ = await invocation.ConfigureAwait(false);
	}

	private static async ValueTask<object?> AwaitTypedResponseAsObjectAsync<TResponse>(Task<TResponse> invocation)
	{
		return await invocation.ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TResponse GetCompletedTaskResult<TResponse>(Task<TResponse> task)
	{
#pragma warning disable CA1849
#pragma warning disable RS0030
		return task.Result;
#pragma warning restore RS0030
#pragma warning restore CA1849
	}

	private static async ValueTask<object?> AwaitNoResponseAsObjectAsync(ValueTask invocation)
	{
		await invocation.ConfigureAwait(false);
		return null;
	}

	private static async ValueTask<TResponse?> AwaitTypedResponseAsync<TResponse>(Task<TResponse> invocation)
	{
		return await invocation.ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TResponse? CastTypedResponse<TResponse>(object? value) =>
		value is TResponse typed ? typed : default;

	private static async ValueTask<TResponse?> AwaitObjectAsTypedResponseAsync<TResponse>(ValueTask<object?> invocation)
	{
		var result = await invocation.ConfigureAwait(false);
		return CastTypedResponse<TResponse>(result);
	}

	private static async Task AwaitWithResponseAsync(ValueTask<object?> invocation, IMessageContext context)
	{
		var result = await invocation.ConfigureAwait(false);
		if (result != null)
		{
			context.Result = result;
		}
	}

	private readonly record struct DirectActionDispatchPlan(
		[property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		Type HandlerType,
		bool ExpectsResponse,
		bool RequiresContext,
		DirectActionNoResponseSyncInvoker? TryInvokeNoResponseSync,
		DirectActionNoResponseAsyncInvoker? InvokeNoResponseAsync,
		DirectActionWithResponseSyncInvoker? TryInvokeWithResponseSync,
		DirectActionWithResponseAsyncInvoker? InvokeWithResponseAsync);

	private readonly record struct PrecompiledDirectActionDispatchPlan(
		bool ExpectsResponse,
		bool RequiresContext,
		PrecompiledDirectInvokeDelegate Invoke);

	private readonly record struct PrecompiledDirectProvider(
		PrecompiledDirectCanHandleDelegate CanHandle,
		PrecompiledDirectTryGetMetadataDelegate TryGetMetadata,
		PrecompiledDirectInvokeDelegate Invoke);

	private enum NoContextActivationMode : byte
	{
		SingletonCached = 0,
		SelfRegistered = 1,
		FactoryActivator = 2,
		GenericActivator = 3,
	}

	private enum ContextActivationMode : byte
	{
		SingletonCached = 0,
		RegisteredOptimized = 1,
		FactoryOptimized = 2,
		GenericActivator = 3,
	}

	private readonly record struct NoContextActivationPlan(
		NoContextActivationMode Mode,
		object? SingletonHandler);

	private readonly record struct ContextActivationPlan(
		ContextActivationMode Mode,
		object? SingletonHandler);

	private readonly record struct EventDispatchPlan(
		bool RequiresContext,
		EventHandlerAsyncInvoker Invoke);

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.NoHandlersForEvent, LogLevel.Warning,
		"No handlers registered for event {EventType}")]
	private partial void LogNoHandlersRegisteredForEvent(string eventType);
}
