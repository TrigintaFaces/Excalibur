// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Transport;

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
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification = "The local bus is the single seam every message kind, handler shape and diagnostic surface passes through.")]
internal sealed partial class LocalMessageBus(
	IServiceProvider provider,
	IHandlerRegistry registry,
	IHandlerActivator activator,
	IHandlerInvoker invoker,
	ILogger<LocalMessageBus> logger) : IMessageBus
{
	private const string ResultContextKey = "Dispatch:Result";
	private const string CacheHitContextKey = "Dispatch:CacheHit";

	// Internal concrete-typed access for performance: avoids IHandlerRegistryEntry → HandlerRegistryEntry casts
	private readonly HandlerRegistry? _concreteRegistry = registry as HandlerRegistry;

	private readonly FrozenDictionary<Type, HandlerRegistryEntry> _frozenHandlerEntryMap =
		InitializeFrozenHandlerEntryMap(registry);

	private readonly FrozenDictionary<Type, HandlerRegistryEntry[]> _frozenEventHandlersMap =
		InitializeFrozenEventHandlersMap(registry);

	private readonly FrozenDictionary<Type, EventDispatchPlan[]> _frozenEventDispatchPlanMap =
		InitializeFrozenEventDispatchPlanMap(registry, logger);

	private readonly FrozenDictionary<Type, DirectActionDispatchPlan> _frozenDirectActionPlanMap =
		InitializeFrozenDirectActionPlanMap(registry, logger);

	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry> _handlerEntryCache = new();

	private readonly ConcurrentDictionary<Type, HandlerRegistryEntry[]> _eventHandlersCache =
		InitializeEventHandlersCache(registry);

	private readonly ConcurrentDictionary<Type, EventDispatchPlan[]?> _eventDispatchPlanCache = new();

	private readonly ConcurrentDictionary<Type, DirectActionDispatchPlan?> _directActionPlanCache =
		InitializeDirectActionPlanCache(registry, logger);

	private readonly ConcurrentDictionary<Type, PrecompiledDirectActionDispatchPlan?> _precompiledDirectActionPlanCache = new();
	private readonly ConcurrentDictionary<Type, bool> _selfRegisteredHandlerCache = new();
	private readonly ConcurrentDictionary<Type, bool> _singletonNoContextEligibilityCache = new();
	private readonly ConcurrentDictionary<Type, object> _singletonNoContextHandlerCache = new();
	private readonly ConcurrentDictionary<Type, bool> _parameterlessStatelessPromotionEligibilityCache = new();
	private readonly ConcurrentDictionary<Type, NoContextActivationPlan> _noContextActivationPlanCache = new();
	private readonly ConcurrentDictionary<Type, ContextActivationPlan> _contextActivationPlanCache = new();
	private readonly ConcurrentDictionary<Type, Func<object>> _noContextResolverCache = new();
	private readonly ConcurrentDictionary<Type, Func<IMessageContext, IServiceProvider, object>> _contextResolverCache = new();

	// PERF: ThreadStatic one-element cache for direct action dispatch plan lookups.
	// Eliminates FrozenDictionary hash computation (~5-10ns) on repeated same-type dispatches.
	[ThreadStatic] private static LocalMessageBus? s_cachedPlanBus;
	[ThreadStatic] private static Type? s_cachedPlanType;
	[ThreadStatic] private static DirectActionDispatchPlan s_cachedPlan;
	[ThreadStatic] private static bool s_cachedPlanValid;

	private readonly IServiceProviderIsService? _serviceProviderIsService =
		provider.GetService(typeof(IServiceProviderIsService)) as IServiceProviderIsService;



	// Scope-correct handler resolution (eliminates the captive-dependency failure where a singleton
	// message bus resolves a scoped handler from the root container). The lifetime decision and scope
	// acquisition live in a dedicated collaborator; the bus keeps only a hot-path verdict cache keyed by
	// message type so the root-resolvable path (transient/singleton handlers) pays no dictionary lookup.
	// Single-handler messages (actions, documents) cache a bool verdict; an event caches the first handler
	// type that requires a scope (the aggregate OR verdict, plus an honest diagnostic anchor) or null.
	private readonly HandlerScopeResolver _scopeResolver = new(provider);
	private readonly ConcurrentDictionary<Type, bool> _messageRequiresScopeCache = new();
	private readonly ConcurrentDictionary<Type, Type?> _eventScopeAnchorCache = new();

	[ThreadStatic] private static LocalMessageBus? s_scopeReqBus;
	[ThreadStatic] private static Type? s_scopeReqType;
	[ThreadStatic] private static bool s_scopeReqValue;

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

	[ThreadStatic] private static LocalMessageBus? s_cachedNoContextBus;
	[ThreadStatic] private static Type? s_cachedNoContextHandlerType;
	[ThreadStatic] private static Func<object>? s_cachedNoContextResolver;
	private static readonly IMessageContext NoContextActivationContext = new MessageContext();

	/// <summary>
	/// Sends a command or action message to its registered handler for processing.
	/// </summary>
	/// <param name="action"> The action/command message to send for processing. </param>
	/// <param name="context"> The message context containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous send operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="action" /> or <paramref name="context" /> is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when no handler is registered for the action type. </exception>
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
			throw CreateMissingHandlerException(messageType);
		}

		// A handler whose dependency graph reaches a scoped service must be resolved from a DI scope, never
		// from the root container this singleton bus captured. The ultra-local and direct fast paths gate on
		// the same verdict; this is the general path taken whenever middleware is registered.
		if (RequiresScope(messageType))
		{
			return SendScopedAsync(entry, action, context, cancellationToken);
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
	/// <remarks>
	/// <para>
	/// <strong>Scope sharing.</strong> All handlers for a single published event observe the <em>same</em>
	/// scoped service instances. When at least one handler for the event must be resolved from a
	/// dependency-injection scope, exactly one scope is used for the whole fan-out — the caller's request
	/// scope when the dispatch carries one, otherwise the ambient scope, otherwise one scope created for the
	/// event and disposed once every handler has completed. When no handler needs a scope, none is opened.
	/// </para>
	/// <para>
	/// <strong>Faults are isolated; state is not.</strong> These are different properties and they must not
	/// be conflated. Every handler is started even if an earlier one throws, and the failures are aggregated,
	/// so one handler's fault never abandons the others. The scoped instances, however, are shared: a handler
	/// that faults after leaving a scoped service in a broken state (an aborted transaction, a
	/// change-tracking context poisoned by a failed save) hands that state to its sibling handlers, which run
	/// afterwards and observe it. A handler that must not inherit a sibling's partial work should either take
	/// its dependency as a factory it resolves per invocation, or be dispatched as its own message.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"IMessageBus carries no ahead-of-time annotation, so this implementation cannot declare one without "
			+ "diverging from the interface it implements. The requirement is declared on the registration that "
			+ "composes this bus, which is where a consumer's compiler reads it.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"IMessageBus carries no ahead-of-time annotation, so this implementation cannot declare one without "
			+ "diverging from the interface it implements. The requirement is declared on the registration that "
			+ "composes this bus, which is where a consumer's compiler reads it.")]
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

		// ONE scope per published event, opened only when at least one handler requires one and shared by
		// every handler for that event -- not one scope per handler. The resolver's precedence decides where
		// it comes from (caller-supplied scope, then ambient, then freshly created); the first two are
		// borrowed and cannot be subdivided per handler, so a per-handler rule would mean different
		// unit-of-work semantics per host.
		var scopeAnchor = GetEventScopeAnchor(messageType, plans);
		if (scopeAnchor is not null)
		{
			await PublishInScopeAsync(scopeAnchor, messageType, evt, plans, context, cancellationToken).ConfigureAwait(false);
			return;
		}

		await PublishToPlansAsync(messageType, evt, plans, context, scopeOpen: false, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the whole fan-out inside a single scope. The scope is acquired once for the event and released
	/// only after every handler has completed: <see cref="PublishToPlansAsync"/> awaits all invocations
	/// before returning, so disposal cannot race a handler that did not complete synchronously.
	/// </summary>
	[RequiresUnreferencedCode("Event dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Event dispatch uses reflection-based dispatch plan resolution.")]
	private async ValueTask PublishInScopeAsync(
		Type scopeAnchor,
		Type messageType,
		IDispatchEvent evt,
		EventDispatchPlan[] plans,
		IMessageContext context,
		CancellationToken cancellationToken)
		=> _ = await _scopeResolver.RunAsync(
			scopeAnchor,
			PreferredScope(context),
			new PublishScopeState(this, messageType, evt, plans, context, cancellationToken),
			// static: the state above carries everything the body needs, so no closure object and no
			// per-dispatch delegate is allocated on this path.
			static async (scopedProvider, s) =>
			{
				// Rebind the caller's context to the resolved scope for the duration of the fan-out (and
				// restore it after) so every handler resolves from that scope and sees a RequestServices
				// matching where it was resolved -- without discarding the context's correlation, items or
				// features, which a substitute context would lose.
				var previous = s.Context.RequestServices;
				s.Context.RequestServices = scopedProvider;
				try
				{
					await s.Bus.PublishToPlansAsync(
						s.MessageType, s.Event, s.Plans, s.Context, scopeOpen: true, s.CancellationToken)
						.ConfigureAwait(false);
				}
				finally
				{
					s.Context.RequestServices = previous;
				}

				return (object?)null;
			}).ConfigureAwait(false);

	/// <summary>
	/// State handed to the scoped fan-out body so it can be a non-capturing <see langword="static"/> lambda.
	/// </summary>
	private readonly record struct PublishScopeState(
		LocalMessageBus Bus,
		Type MessageType,
		IDispatchEvent Event,
		EventDispatchPlan[] Plans,
		IMessageContext Context,
		CancellationToken CancellationToken);

	/// <summary>
	/// Invokes every dispatch plan for the event. When <paramref name="scopeOpen"/> is set, a scope is
	/// already held for this event and the handlers that require one are given the (scope-bound) context so
	/// they resolve from it; handlers that do not require a scope keep their existing no-context resolution
	/// path, so a shared scope never costs them their singleton/no-context bypass.
	/// </summary>
	[RequiresUnreferencedCode("Event dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Event dispatch uses reflection-based dispatch plan resolution.")]
	private async ValueTask PublishToPlansAsync(
		Type messageType,
		IDispatchEvent evt,
		EventDispatchPlan[] plans,
		IMessageContext context,
		bool scopeOpen,
		CancellationToken cancellationToken)
	{
		if (plans.Length == 1)
		{
			var singlePlan = plans[0];
			var singleInvocation = singlePlan.Invoke(
				this,
				evt,
				ResolvePlanContext(singlePlan, context, scopeOpen),
				cancellationToken);
			if (!singleInvocation.IsCompletedSuccessfully)
			{
				await singleInvocation.ConfigureAwait(false);
			}

			return;
		}

		// Fan-out with fault isolation: start EVERY handler, await ALL of them, and aggregate faults.
		// One handler's failure must not abandon the others -- IEventHandler fault-independence is part of
		// the IMessageBus event contract. Collected faults surface together as an AggregateException.
		// (Contrast the single-plan fast path above, which throws its sole handler's exception directly:
		// a single-handler event is not wrapped in an AggregateException.)
		var invocations = ArrayPool<ValueTask>.Shared.Rent(plans.Length);
		List<Exception>? faults = null;
		try
		{
			for (var i = 0; i < plans.Length; i++)
			{
				var plan = plans[i];
				try
				{
					var invocation = plan.Invoke(
						this,
						evt,
						ResolvePlanContext(plan, context, scopeOpen),
						cancellationToken);
					invocations[i] = invocation;
				}
#pragma warning disable CA1031 // Fault-independence: isolate a synchronously-throwing handler; rethrown aggregated below.
				catch (Exception ex)
				{
					// A handler that throws synchronously must not prevent the remaining handlers from running.
					invocations[i] = ValueTask.CompletedTask;
					(faults ??= []).Add(ex);
				}
#pragma warning restore CA1031
			}

			for (var i = 0; i < plans.Length; i++)
			{
				try
				{
					await invocations[i].ConfigureAwait(false);
				}
#pragma warning disable CA1031 // Fault-independence: collect each handler fault; rethrown aggregated below.
				catch (Exception ex)
				{
					(faults ??= []).Add(ex);
				}
#pragma warning restore CA1031
			}
		}
		finally
		{
			Array.Clear(invocations, 0, plans.Length);
			ArrayPool<ValueTask>.Shared.Return(invocations, clearArray: false);
		}

		if (faults is not null)
		{
			// A single fault surfaces as itself, not wrapped: the number of registered handlers must not
			// change which exception type a consumer sees, or an exception handler / mapper registered for
			// the domain exception stops matching the moment a second handler is registered. Only a genuine
			// multi-fault fan-out aggregates. (Same unwrap rule as Task.GetAwaiter().GetResult().)
			if (faults.Count == 1)
			{
				ExceptionDispatchInfo.Capture(faults[0]).Throw();
			}

			throw new AggregateException(
				$"One or more handlers failed while publishing event '{messageType.Name}'.",
				faults);
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
	public Task SendDocumentAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = doc.GetType();
		if (!TryGetHandlerEntry(messageType, out var entry))
		{
			throw CreateMissingHandlerException(messageType);
		}

		// Same rule as the action path: a handler that reaches a scoped service resolves from a DI scope,
		// never from the root container this singleton bus captured.
		if (RequiresScope(messageType))
		{
			return SendDocumentScopedAsync(entry.HandlerType, doc, context, cancellationToken);
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

	/// <summary>
	/// Builds the exception thrown when a message reaches the local bus with no handler registered for it.
	/// </summary>
	/// <param name="messageType"> The message type that has no registered handler. </param>
	/// <returns> An exception naming the message type and both ways to register a handler for it. </returns>
	internal static HandlerNotRegisteredException CreateMissingHandlerException(Type messageType)
	{
		var isDocument = typeof(IDispatchDocument).IsAssignableFrom(messageType);
		var kind = isDocument ? "document" : "action";
		var handlerInterface = isDocument ? "IDocumentHandler" : "IActionHandler";

		return new HandlerNotRegisteredException(
			$"No handler registered for {kind} '{messageType.FullName}'. " +
			$"Did you forget to call services.AddDispatch(d => d.AddHandlersFromAssembly(typeof({messageType.Name}).Assembly))? " +
			$"Alternatively, register the handler directly with services.AddTransient<{handlerInterface}<{messageType.Name}>, YourHandler>().");
	}

	/// <summary>
	/// Determines whether a handler is registered for the supplied message type.
	/// </summary>
	/// <param name="messageType"> The message type to look up. </param>
	/// <returns> <see langword="true" /> when a handler is registered; otherwise <see langword="false" />. </returns>
	/// <remarks>
	/// Dispatch paths that convert handler failures into a failed result call this before invoking the bus, so that a missing
	/// registration — a configuration fault rather than a runtime outcome — is raised outside that conversion and reaches the caller.
	/// </remarks>
	internal bool HasHandlerFor(Type messageType) => TryGetHandlerEntry(messageType, out _);

	[RequiresUnreferencedCode("Direct invocation uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Direct invocation uses reflection-based dispatch plan resolution.")]
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

		// Scoped handlers must resolve from a DI scope, not the root container captured by this
		// singleton bus. Deterministic + cached; the root-resolvable hot path never enters here.
		if (RequiresScope(actionType) && TryGetHandlerEntry(actionType, out var scopedEntry))
		{
			invocation = InvokeScopedObjectAsync(actionType, scopedEntry.HandlerType, action, context, cancellationToken);
			return true;
		}

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

	[RequiresUnreferencedCode("Direct invocation uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Direct invocation uses reflection-based dispatch plan resolution.")]
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

		// Scoped handlers must resolve from a DI scope, not the root container captured by this
		// singleton bus. Deterministic + cached; the root-resolvable hot path never enters here.
		if (RequiresScope(actionType) && TryGetHandlerEntry(actionType, out var scopedEntry))
		{
			invocation = InvokeScopedNoResponseAsync(actionType, scopedEntry.HandlerType, action, context, cancellationToken);
			return true;
		}

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

	[RequiresUnreferencedCode("Ultra-local dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Ultra-local dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocal(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation)
	{
		return TryInvokeUltraLocal(action, cancellationToken, out invocation, out _);
	}

	[RequiresUnreferencedCode("Ultra-local typed dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Ultra-local typed dispatch uses reflection-based dispatch plan resolution.")]
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

		// Scoped handlers are not eligible for the no-context ultra-local fast path: they must resolve from
		// the dispatch context's request scope (shared request-scoped state). Decline and report
		// requiresContext so the dispatcher routes through the context-aware path (TryInvokeDirect with the
		// caller's context, or a lazily-rented context off-request), which prefers that scope.
		if (RequiresScope(actionType))
		{
			requiresContext = true;
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

	[RequiresUnreferencedCode("Ultra-local dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Ultra-local dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocal(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation,
		out bool requiresContext)
	{
		ArgumentNullException.ThrowIfNull(action);

		var actionType = action.GetType();

		// Scoped handlers are not eligible for the no-context ultra-local fast path: they must resolve from
		// the dispatch context's request scope (shared request-scoped state). Decline here and report
		// requiresContext so the dispatcher routes through the context-aware path (TryInvokeDirect with the
		// caller's context, or a lazily-rented context off-request), which prefers that scope.
		if (RequiresScope(actionType))
		{
			requiresContext = true;
			invocation = default;
			return false;
		}

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

				invocation = precompiledPlan.Invoke(action, provider, null, cancellationToken);
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

	[RequiresUnreferencedCode("Ultra-local no-response dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Ultra-local no-response dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalNoResponse(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask invocation)
	{
		return TryInvokeUltraLocalNoResponse(action, cancellationToken, out invocation, out _);
	}

	[RequiresUnreferencedCode("Ultra-local no-response dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Ultra-local no-response dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalNoResponse(
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask invocation,
		out bool requiresContext)
	{
		ArgumentNullException.ThrowIfNull(action);

		var actionType = action.GetType();

		// Scoped handlers are not eligible for the no-context ultra-local fast path: they must resolve from
		// the dispatch context's request scope (shared request-scoped state). Decline here and report
		// requiresContext so the dispatcher routes through the context-aware path (TryInvokeDirectNoResponse
		// with the caller's context, or a lazily-rented context off-request), which prefers that scope.
		if (RequiresScope(actionType))
		{
			requiresContext = true;
			invocation = default;
			return false;
		}

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

				var precompiledInvocation = precompiledPlan.Invoke(action, provider, null, cancellationToken);
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

	/// <summary>
	/// Fast-path ultra-local no-response invocation for pre-validated dispatch types.
	/// </summary>
	/// <remarks>
	/// Callers must ensure <paramref name="actionType"/> is eligible for no-context, no-response dispatch.
	/// This method intentionally avoids metadata resolution work on the hot path.
	/// </remarks>
	[RequiresUnreferencedCode("Fast-path dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Fast-path dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalNoResponseFast(
		Type actionType,
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask invocation)
	{
		ArgumentNullException.ThrowIfNull(actionType);
		ArgumentNullException.ThrowIfNull(action);

		// Scoped handlers must resolve from a DI scope, not the root container captured by this
		// singleton bus. Deterministic + cached; the root-resolvable hot path never enters here.
		if (RequiresScope(actionType) && TryGetHandlerEntry(actionType, out var scopedEntry))
		{
			invocation = InvokeScopedNoResponseAsync(actionType, scopedEntry.HandlerType, action, context: null, cancellationToken);
			return true;
		}

		if (TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (resolvedPlan.ExpectsResponse || resolvedPlan.RequiresContext)
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

		if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
		{
			if (precompiledPlan.ExpectsResponse || ResolveRequiresContext(actionType, precompiledPlan.RequiresContext))
			{
				invocation = default;
				return false;
			}

			var precompiledInvocation = precompiledPlan.Invoke(action, provider, null, cancellationToken);
			invocation = precompiledInvocation.IsCompletedSuccessfully
				? ValueTask.CompletedTask
				: AwaitNoResponseValueTaskAsync(precompiledInvocation);
			return true;
		}

		invocation = default;
		return false;
	}

	/// <summary>
	/// Fast-path ultra-local with-response invocation for pre-validated dispatch types.
	/// </summary>
	/// <remarks>
	/// Callers must ensure <paramref name="actionType"/> is eligible for no-context, with-response dispatch.
	/// This method intentionally avoids metadata resolution work on the hot path.
	/// </remarks>
	[RequiresUnreferencedCode("Fast-path dispatch uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Fast-path dispatch uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryInvokeUltraLocalWithResponseFast(
		Type actionType,
		IDispatchAction action,
		CancellationToken cancellationToken,
		out ValueTask<object?> invocation)
	{
		ArgumentNullException.ThrowIfNull(actionType);
		ArgumentNullException.ThrowIfNull(action);

		// Scoped handlers must resolve from a DI scope, not the root container captured by this
		// singleton bus. Deterministic + cached; the root-resolvable hot path never enters here.
		if (RequiresScope(actionType) && TryGetHandlerEntry(actionType, out var scopedEntry))
		{
			invocation = InvokeScopedObjectAsync(actionType, scopedEntry.HandlerType, action, context: null, cancellationToken);
			return true;
		}

		if (TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			if (!resolvedPlan.ExpectsResponse || resolvedPlan.RequiresContext)
			{
				invocation = default;
				return false;
			}

			if (resolvedPlan.TryInvokeWithResponseSync is { } syncInvoker)
			{
				if (syncInvoker(this, action, context: null, cancellationToken, out var result, out var pendingInvocation))
				{
					invocation = new ValueTask<object?>(result);
					return true;
				}

				invocation = pendingInvocation;
				return true;
			}

			if (resolvedPlan.InvokeWithResponseAsync is null)
			{
				invocation = default;
				return false;
			}

			invocation = resolvedPlan.InvokeWithResponseAsync(this, action, context: null, cancellationToken);
			return true;
		}

		if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
		{
			if (!precompiledPlan.ExpectsResponse || ResolveRequiresContext(actionType, precompiledPlan.RequiresContext))
			{
				invocation = default;
				return false;
			}

			invocation = precompiledPlan.Invoke(action, provider, null, cancellationToken);
			return true;
		}

		invocation = default;
		return false;
	}

	/// <summary>
	/// Resolves prevalidated ultra-local invokers for a direct action type.
	/// </summary>
	[RequiresUnreferencedCode("Fast invoker resolution uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Fast invoker resolution uses reflection-based dispatch plan resolution.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetUltraLocalFastInvokers(
		Type actionType,
		out bool expectsResponse,
		out bool requiresContext,
		out Func<IDispatchAction, CancellationToken, ValueTask>? noResponseInvoker,
		out Func<IDispatchAction, CancellationToken, ValueTask<object?>>? withResponseInvoker)
	{
		ArgumentNullException.ThrowIfNull(actionType);

		// Scoped handlers must resolve from a DI scope, not the root container captured by this singleton
		// bus. Emit scope-aware invokers (each creates/borrows a scope per invocation); the root-resolvable
		// hot path never enters here.
		if (RequiresScope(actionType) && TryGetHandlerEntry(actionType, out var scopedEntry))
		{
			// Scoped handlers must route through the context-aware dispatch path (TryInvokeDirect*) so the
			// dispatch context's request scope (IMessageContext.RequestServices) is honored and shared with
			// the rest of the request. A cached no-context fast invoker would resolve from a fresh scope,
			// breaking request-scope sharing. Reporting requiresContext opts out of that no-context fast
			// path; the context path then prefers the context's request scope (else ambient, else fresh).
			expectsResponse = scopedEntry.ExpectsResponse;
			requiresContext = true;
			noResponseInvoker = null;
			withResponseInvoker = null;
			return true;
		}

		if (TryGetDirectActionDispatchPlan(actionType, out var resolvedPlan))
		{
			expectsResponse = resolvedPlan.ExpectsResponse;
			requiresContext = resolvedPlan.RequiresContext;

			if (requiresContext)
			{
				noResponseInvoker = null;
				withResponseInvoker = null;
				return true;
			}

			if (expectsResponse)
			{
				if (resolvedPlan.TryInvokeWithResponseSync is { } syncInvoker)
				{
					withResponseInvoker = (action, cancellationToken) =>
					{
						return syncInvoker(this, action, context: null, cancellationToken, out var result, out var pendingInvocation)
							? new ValueTask<object?>(result)
							: pendingInvocation;
					};
					noResponseInvoker = null;
					return true;
				}

				if (resolvedPlan.InvokeWithResponseAsync is { } asyncInvoker)
				{
					withResponseInvoker = (action, cancellationToken) => asyncInvoker(this, action, context: null, cancellationToken);
					noResponseInvoker = null;
					return true;
				}

				noResponseInvoker = null;
				withResponseInvoker = null;
				return true;
			}

			if (resolvedPlan.TryInvokeNoResponseSync is { } syncNoResponseInvoker)
			{
				noResponseInvoker = (action, cancellationToken) =>
				{
					_ = syncNoResponseInvoker(this, action, context: null, cancellationToken, out var pendingInvocation);
					return pendingInvocation;
				};
				withResponseInvoker = null;
				return true;
			}

			if (resolvedPlan.InvokeNoResponseAsync is { } asyncNoResponseInvoker)
			{
				noResponseInvoker = (action, cancellationToken) => asyncNoResponseInvoker(this, action, context: null, cancellationToken);
				withResponseInvoker = null;
				return true;
			}

			noResponseInvoker = null;
			withResponseInvoker = null;
			return true;
		}

		if (TryGetPrecompiledDirectActionDispatchPlan(actionType, out var precompiledPlan))
		{
			expectsResponse = precompiledPlan.ExpectsResponse;
			requiresContext = ResolveRequiresContext(actionType, precompiledPlan.RequiresContext);
			if (requiresContext)
			{
				noResponseInvoker = null;
				withResponseInvoker = null;
				return true;
			}

			if (expectsResponse)
			{
				withResponseInvoker = (action, cancellationToken) =>
					precompiledPlan.Invoke(action, provider, null, cancellationToken);
				noResponseInvoker = null;
				return true;
			}

			noResponseInvoker = (action, cancellationToken) =>
			{
				var precompiledInvocation = precompiledPlan.Invoke(action, provider, null, cancellationToken);
				return precompiledInvocation.IsCompletedSuccessfully
					? ValueTask.CompletedTask
					: AwaitNoResponseValueTaskAsync(precompiledInvocation);
			};
			withResponseInvoker = null;
			return true;
		}

		expectsResponse = false;
		requiresContext = false;
		noResponseInvoker = null;
		withResponseInvoker = null;
		return false;
	}

	[RequiresUnreferencedCode("Dispatch metadata resolution uses reflection-based dispatch plan resolution.")]
	[RequiresDynamicCode("Dispatch metadata resolution uses reflection-based dispatch plan resolution.")]
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

	[RequiresUnreferencedCode("Dispatch plan resolution may create typed invokers via reflection when not cached.")]
	[RequiresDynamicCode("Dispatch plan resolution may create typed invokers via MakeGenericMethod when not cached.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetDirectActionDispatchPlan(
		Type actionType,
		out DirectActionDispatchPlan resolvedPlan)
	{
		// PERF: ThreadStatic one-element cache for repeated same-type dispatches.
		if (ReferenceEquals(s_cachedPlanBus, this) &&
			ReferenceEquals(s_cachedPlanType, actionType) &&
			s_cachedPlanValid)
		{
			resolvedPlan = s_cachedPlan;
			return true;
		}

		if (_frozenDirectActionPlanMap.TryGetValue(actionType, out var frozen))
		{
			s_cachedPlanBus = this;
			s_cachedPlanType = actionType;
			s_cachedPlan = frozen;
			s_cachedPlanValid = true;
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

		s_cachedPlanBus = this;
		s_cachedPlanType = actionType;
		s_cachedPlan = plan.Value;
		s_cachedPlanValid = true;
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
			if (plan.TryInvokeWithResponseSync!(this, action, context, cancellationToken, out var result, out var pending))
			{
				return new ValueTask<object?>(result);
			}

			return pending;
		}

		if (plan.TryInvokeNoResponseSync!(this, action, context, cancellationToken, out var pendingNoResponse))
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

		var allHandlers = GetConcreteEntries(registry);
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

	[RequiresUnreferencedCode("Event dispatch plan resolution may create typed invokers via reflection.")]
	[RequiresDynamicCode("Event dispatch plan resolution may create typed invokers via reflection.")]
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

		var created = CreateEventDispatchPlans(handlers, logger);
		_ = _eventDispatchPlanCache.TryAdd(messageType, created);
		return created;
	}

	private static EventDispatchPlan[] CreateEventDispatchPlans(HandlerRegistryEntry[] handlers, ILogger logger)
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
			if (TryCreateTypedEventAsyncInvoker(entry.MessageType, entry.HandlerType, logger, out var typedInvoker))
			{
				invoker = typedInvoker;
			}
			else
			{
				// Unknown/legacy handler shapes stay on the context-aware fallback path.
				requiresContext = true;
				invoker = CreateEventAsyncInvoker(entry.HandlerType);
			}

			plans[index] = new EventDispatchPlan(entry.HandlerType, requiresContext, invoker);
		}

		return plans;
	}

	[RequiresDynamicCode("Creates direct action dispatch plans via reflection-based typed invoker construction.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private DirectActionDispatchPlan? CreateDirectActionDispatchPlan(Type actionType)
	{
		if (!TryGetHandlerEntry(actionType, out var entry))
		{
			return null;
		}

		return CreateRuntimeDirectActionDispatchPlan(entry, logger);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionDispatchPlan CreateRuntimeDirectActionDispatchPlan(HandlerRegistryEntry entry, ILogger logger)
	{
		if (TryCreateTypedDirectActionDispatchPlan(entry, logger, out var typedPlan))
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
		ILogger logger,
		out DirectActionDispatchPlan plan)
	{
		if (!entry.ExpectsResponse)
		{
			if (!TryCreateTypedNoResponseAsyncInvoker(entry.MessageType, entry.HandlerType, logger, out var invokeNoResponseAsync))
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
			!TryCreateTypedWithResponseAsyncInvoker(entry.MessageType, entry.HandlerType, responseType, logger, out var invokeWithResponseAsync))
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

	private static bool TryGetActionResponseType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type actionType, out Type responseType)
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

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"The generic construction below is behind an IsDynamicCodeSupported guard: where dynamic code is "
			+ "unavailable this declines and the caller dispatches through the source-generated invoker instead. "
			+ "Declaring the requirement would push it up the composition chain to every consumer, including the "
			+ "ones this guard exists to serve.")]
	private static bool TryCreateTypedNoResponseAsyncInvoker(
		Type actionType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		ILogger logger,
		out DirectActionNoResponseAsyncInvoker invoker)
	{
		// Building a typed invoker needs runtime code generation. Under AOT, decline so the caller
		// falls back to the context-aware path, which dispatches through IHandlerInvoker (source-generated).
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			invoker = null!;
			return false;
		}

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
			LogTypedInvokerBuildFailed(logger, ex, handlerType.FullName ?? handlerType.Name);
			invoker = null!;
			return false;
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"The generic construction below is behind an IsDynamicCodeSupported guard: where dynamic code is "
			+ "unavailable this declines and the caller dispatches through the source-generated invoker instead. "
			+ "Declaring the requirement would push it up the composition chain to every consumer, including the "
			+ "ones this guard exists to serve.")]
	private static bool TryCreateTypedWithResponseAsyncInvoker(
		Type actionType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		Type responseType,
		ILogger logger,
		out DirectActionWithResponseAsyncInvoker invoker)
	{
		// Building a typed invoker needs runtime code generation. Under AOT, decline so the caller
		// falls back to the context-aware path, which dispatches through IHandlerInvoker (source-generated).
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			invoker = null!;
			return false;
		}

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
			LogTypedInvokerBuildFailed(logger, ex, handlerType.FullName ?? handlerType.Name);
			invoker = null!;
			return false;
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"The generic construction below is behind an IsDynamicCodeSupported guard: where dynamic code is "
			+ "unavailable this declines and the caller dispatches through the source-generated invoker instead. "
			+ "Declaring the requirement would push it up the composition chain to every consumer, including the "
			+ "ones this guard exists to serve.")]
	private static bool TryCreateTypedEventAsyncInvoker(
		Type eventType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		ILogger logger,
		out EventHandlerAsyncInvoker invoker)
	{
		// Building a typed invoker needs runtime code generation. Under AOT, decline so the caller
		// falls back to the context-aware path, which dispatches through IHandlerInvoker (source-generated).
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			invoker = null!;
			return false;
		}

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
			LogTypedInvokerBuildFailed(logger, ex, handlerType.FullName ?? handlerType.Name);
			invoker = null!;
			return false;
		}
	}

	private static DirectActionNoResponseAsyncInvoker CreateTypedNoResponseAsyncInvokerCore<TAction, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TAction : IDispatchAction
		where THandler : IActionHandler<TAction>
	{
		return static (bus, action, context, cancellationToken) =>
			bus.InvokeTypedNoResponse<TAction, THandler>(action, context, cancellationToken);
	}

	private static DirectActionWithResponseAsyncInvoker CreateTypedWithResponseAsyncInvokerCore<TAction, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TResponse>()
		where TAction : IDispatchAction<TResponse>
		where THandler : IActionHandler<TAction, TResponse>
	{
		return static (bus, action, context, cancellationToken) =>
			bus.InvokeTypedWithResponse<TAction, THandler, TResponse>(action, context, cancellationToken);
	}

	private static EventHandlerAsyncInvoker CreateTypedEventAsyncInvokerCore<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
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

	// Providers are discovered via PrecompiledDirectDispatchRegistry, which each consuming assembly's
	// source-generated PrecompiledDirectActionDispatch class populates through a [ModuleInitializer] at
	// module load. No reflection: the registration call site is ordinary, trimmer-visible code, so the
	// generated type's absence is simply "nothing registered" rather than a silent reflective fallback.
	private static PrecompiledDirectActionDispatchPlan? ResolvePrecompiledDirectActionDispatchPlan(Type actionType)
	{
		var providers = PrecompiledDirectDispatchRegistry.GetAll();
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionNoResponseSyncInvoker CreateDirectActionNoResponseSyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		return (bus, action, context, cancellationToken) =>
			bus.InvokeDirectActionNoResponse(handlerType, action, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static DirectActionWithResponseSyncInvoker CreateDirectActionWithResponseSyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		return (bus, action, context, cancellationToken) =>
			bus.InvokeDirectAction(handlerType, action, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static EventHandlerAsyncInvoker CreateEventAsyncInvoker(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		return (bus, evt, context, cancellationToken) =>
			bus.InvokeEventHandler(handlerType, evt, context, cancellationToken);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ValueTask InvokeDirectActionNoResponse(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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

	/// <summary>
	/// Returns whether the single handler for <paramref name="messageType"/> (an action or a document) must
	/// be resolved from a dependency-injection scope rather than the root container captured by this
	/// singleton bus. Deterministic and cached per message type; the warm path is a ThreadStatic compare so
	/// the root-resolvable hot path (transient/singleton handlers) pays no dictionary lookup.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool RequiresScope(Type messageType)
	{
		if (ReferenceEquals(s_scopeReqBus, this) && ReferenceEquals(s_scopeReqType, messageType))
		{
			return s_scopeReqValue;
		}

		var requiresScope = _messageRequiresScopeCache.GetOrAdd(
			messageType,
			static (type, self) => self.ResolveMessageScopeVerdict(type),
			this);

		s_scopeReqBus = this;
		s_scopeReqType = messageType;
		s_scopeReqValue = requiresScope;
		return requiresScope;
	}

	private bool ResolveMessageScopeVerdict(Type messageType)
		=> TryGetHandlerEntry(messageType, out var entry) && _scopeResolver.RequiresScope(entry.HandlerType);

	/// <summary>
	/// Returns the first handler for <paramref name="eventType"/> that must be resolved from a
	/// dependency-injection scope, or <see langword="null"/> when no handler for the event needs one. The
	/// verdict is the OR over the event's handlers — one scope is opened for the event if <em>any</em>
	/// handler requires it — and is cached per event type. The returned type is also the diagnostic anchor
	/// naming a handler that genuinely required the scope. Each handler's own verdict is fail-safe: the
	/// resolver biases an unprovable dependency graph to Scope.
	/// </summary>
	private Type? GetEventScopeAnchor(Type eventType, EventDispatchPlan[] plans)
	{
		if (_eventScopeAnchorCache.TryGetValue(eventType, out var cached))
		{
			return cached;
		}

		Type? anchor = null;
		for (var index = 0; index < plans.Length; index++)
		{
			var handlerType = plans[index].HandlerType;
			if (_scopeResolver.RequiresScope(handlerType))
			{
				anchor = handlerType;
				break;
			}
		}

		_ = _eventScopeAnchorCache.TryAdd(eventType, anchor);
		return anchor;
	}

	/// <summary>
	/// Chooses the context a single event dispatch plan is invoked with. A handler that requires context
	/// injection always gets it. When a scope is open for the event, a handler that requires a scope also
	/// gets the (scope-bound) context, because that is what routes its activation through the scope's
	/// provider. Every other handler keeps <see langword="null"/> and its existing no-context resolution —
	/// an open scope must not cost a root-safe handler its singleton/no-context bypass.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private IMessageContext? ResolvePlanContext(EventDispatchPlan plan, IMessageContext context, bool scopeOpen)
		=> plan.RequiresContext || (scopeOpen && _scopeResolver.RequiresScope(plan.HandlerType))
			? context
			: null;

	/// <summary>
	/// Invokes a single-handler message (an action or a document) inside the scope the resolver selects,
	/// rebinding the caller's context to that scope for the duration of the invocation so the handler
	/// resolves from it while keeping the context's correlation, items, features and result.
	/// </summary>
	private ValueTask<object?> InvokeInScopeAsync(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		=> _scopeResolver.RunAsync(
			handlerType,
			PreferredScope(context),
			new InvokeScopeState(this, handlerType, message, context, cancellationToken),
			// static: see PublishScopeState -- this is the single-handler dispatch's hot path.
			static async (scopedProvider, s) =>
			{
				var previous = s.Context.RequestServices;
				s.Context.RequestServices = scopedProvider;
				try
				{
					var handler = s.Bus.ActivateHandler(s.HandlerType, s.Context);
					return await s.Bus.InvokeHandler(handler, s.Message, s.CancellationToken).ConfigureAwait(false);
				}
				finally
				{
					s.Context.RequestServices = previous;
				}
			});

	/// <summary>
	/// State handed to the single-handler scoped invocation body so it can be a non-capturing
	/// <see langword="static"/> lambda.
	/// </summary>
	private readonly record struct InvokeScopeState(
		LocalMessageBus Bus,
		// param and field as well as property. Annotating only the generated property leaves the
		// constructor parameter and its backing field unannotated, so the caller's annotated Type is
		// stored somewhere trimming believes carries no requirement -- which is what IL2069 reports.
		[param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type HandlerType,
		IDispatchMessage Message,
		IMessageContext Context,
		CancellationToken CancellationToken);

	private async Task SendScopedAsync(
		HandlerRegistryEntry entry,
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		var result = await InvokeInScopeAsync(entry.HandlerType, action, context, cancellationToken).ConfigureAwait(false);
		if (entry.ExpectsResponse && result is not null)
		{
			context.Result = result;
		}
	}

	private async Task SendDocumentScopedAsync(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IDispatchDocument doc,
		IMessageContext context,
		CancellationToken cancellationToken)
		=> _ = await InvokeInScopeAsync(handlerType, doc, context, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Returns the caller-supplied request scope to prefer for scoped resolution: the dispatch context's
	/// <see cref="IMessageContext.RequestServices"/> when it is a real scope (not the captured root
	/// provider), otherwise <see langword="null"/> so the resolver falls back to the ambient or a fresh
	/// scope. Using the context's own request scope keeps the handler in the same scope as the caller
	/// (shared request-scoped state and a matching <see cref="IMessageContext.RequestServices"/>).
	/// </summary>
	private IServiceProvider? PreferredScope(IMessageContext? context)
	{
		var requestServices = context?.RequestServices;

		// Two things disqualify a caller-supplied provider from being treated as a scope.
		//
		// The reference check catches the provider this bus was constructed with. On its own it is not
		// enough: that provider is the root ENGINE scope, a different object from the ServiceProvider a
		// consumer gets back from BuildServiceProvider(). A worker or console app that builds a provider
		// and constructs a context from it — new MessageContext(message, provider) — therefore had its
		// root accepted as a request scope, and every scoped dependency resolved from it was captive,
		// shared across every dispatch.
		//
		// The IServiceScope test closes that. Measured against Microsoft.Extensions.DependencyInjection:
		// the object returned by BuildServiceProvider() (ServiceProvider) is NOT an IServiceScope, while
		// a real scope's provider (ServiceProviderEngineScope) is. So a consumer-passed root is rejected
		// and a genuine scope — including the ASP.NET Core request scope — is accepted.
		//
		// The failure direction is deliberate. A container whose scope provider does not implement
		// IServiceScope is rejected here and the resolver falls through to creating a fresh scope: correct,
		// marginally more expensive, and never captive. Being wrong toward an extra scope is the safe way
		// to be wrong; being wrong toward sharing one is the defect this replaces.
		return requestServices is not null
			&& !ReferenceEquals(requestServices, provider)
			&& requestServices is IServiceScope
				? requestServices
				: null;
	}

	[RequiresUnreferencedCode("Falls back to reflection-based dispatch plan resolution when no precompiled plan covers the action.")]
	private ValueTask<object?> InvokeScopedObjectAsync(
		Type actionType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
		=> _scopeResolver.RunAsync(
			handlerType,
			PreferredScope(context),
			new ScopedActionState(this, actionType, handlerType, action, context, cancellationToken),
			// static: see PublishScopeState.
			static async (scopedProvider, s) =>
			{
				// Rebind the caller's context to the resolved scope for the duration of the invocation (and
				// restore it after) so the handler resolves from that scope and sees a RequestServices
				// matching where it was resolved -- the same treatment the event fan-out above gives its
				// context, and for the same reason: substituting a fresh context here discarded the caller's
				// tenant, user, correlation and causation outright. A handler resolved into a fresh scope
				// therefore ran untenanted, and silently, because a context carrying no tenant is
				// indistinguishable downstream from one for a genuinely untenanted operation. Only a null
				// caller context needs a substitute, and it has nothing to lose.
				var scopedContext = s.Context ?? new MessageContext(s.Action, scopedProvider);
				var previousServices = scopedContext.RequestServices;
				scopedContext.RequestServices = scopedProvider;

				try
				{
					// Prefer the source-generated precompiled plan (AOT-safe, no reflection), resolved from the
					// scope provider; fall back to activator-based resolution (also from the scope) otherwise.
					if (s.Bus.TryGetPrecompiledDirectActionDispatchPlan(s.ActionType, out var precompiledPlan) &&
						!s.Bus.ResolveRequiresContext(s.ActionType, precompiledPlan.RequiresContext))
					{
						return await precompiledPlan.Invoke(s.Action, scopedProvider, scopedContext, s.CancellationToken).ConfigureAwait(false);
					}

					var handler = s.Bus.ActivateHandler(s.HandlerType, scopedContext);
					return await s.Bus.InvokeHandler(handler, s.Action, s.CancellationToken).ConfigureAwait(false);
				}
				finally
				{
					scopedContext.RequestServices = previousServices;
				}
			});

	/// <summary>
	/// State handed to the scoped action-invocation body so it can be a non-capturing
	/// <see langword="static"/> lambda.
	/// </summary>
	private readonly record struct ScopedActionState(
		LocalMessageBus Bus,
		Type ActionType,
		// param and field as well as property. Annotating only the generated property leaves the
		// constructor parameter and its backing field unannotated, so the caller's annotated Type is
		// stored somewhere trimming believes carries no requirement -- which is what IL2069 reports.
		[param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type HandlerType,
		IDispatchAction Action,
		IMessageContext? Context,
		CancellationToken CancellationToken);

	[RequiresUnreferencedCode("Falls back to reflection-based dispatch plan resolution when no precompiled plan covers the action.")]
	private async ValueTask InvokeScopedNoResponseAsync(
		Type actionType,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		IDispatchAction action,
		IMessageContext? context,
		CancellationToken cancellationToken)
		=> _ = await InvokeScopedObjectAsync(actionType, handlerType, action, context, cancellationToken).ConfigureAwait(false);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private object ResolveHandlerWithoutContext(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		if (ReferenceEquals(s_cachedNoContextBus, this) &&
			ReferenceEquals(s_cachedNoContextHandlerType, handlerType) &&
			s_cachedNoContextResolver is { } cachedResolver)
		{
			return cachedResolver();
		}

		if (!_noContextResolverCache.TryGetValue(handlerType, out var resolver))
		{
			resolver = BuildNoContextResolver(handlerType);
			_ = _noContextResolverCache.TryAdd(handlerType, resolver);
		}

		s_cachedNoContextBus = this;
		s_cachedNoContextHandlerType = handlerType;
		s_cachedNoContextResolver = resolver;
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
	private object ActivateHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		// A scoped handler must never be cached as a process-lifetime singleton, nor resolved from the
		// root container — exclude it from the singleton/no-context bypass so it flows through the
		// scope-aware resolution paths.
		return IsSelfRegisteredHandler(handlerType)
			&& !HandlerActivator.RequiresContextInjection(handlerType)
			&& !_scopeResolver.RequiresScope(handlerType);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryGetSingletonNoContextHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType,
		out object handler)
	{
		if (_singletonNoContextHandlerCache.TryGetValue(handlerType, out handler!))
		{
			return true;
		}

		if (!CanUseSingletonNoContextBypass(handlerType))
		{
			if (TryPromoteTransientStatelessNoContextHandler(handlerType, out handler))
			{
				return true;
			}

			handler = default!;
			return false;
		}

		try
		{
			var first = provider.GetRequiredService(handlerType);
			var second = provider.GetRequiredService(handlerType);
			if (!ReferenceEquals(first, second))
			{
				if (TryPromoteTransientStatelessNoContextHandler(handlerType, out handler))
				{
					return true;
				}

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
	private bool TryPromoteTransientStatelessNoContextHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType,
		out object handler)
	{
		if (!CanPromoteTransientStatelessNoContextHandler(handlerType))
		{
			handler = default!;
			return false;
		}

		try
		{
			// GetService, not GetRequiredService, and an activation fallback behind it. A handler
			// registered the documented way -- AddTransient<IActionHandler<TAction>, THandler>() -- is not
			// resolvable by its concrete type at all, so asking the container for one returns null. That is
			// not a reason to decline the promotion: eligibility above has already established a public
			// parameterless constructor, no instance fields and no context injection.
			//
			// The fallback is the container's own activation algorithm called directly. ActivatorUtilities
			// performs the same constructor selection and argument resolution the provider would have
			// performed had the concrete type been registered, so the promoted instance is
			// framework-constructed -- it does not round-trip through DI -- but for a handler of this shape
			// it is not a different object from the one a registration would have produced. handlerType
			// arrives annotated with the constructor members the activation needs, so the trimming and
			// ahead-of-time contracts are satisfied at the call site and nothing propagates onto the
			// dispatch path. A constructor that cannot be satisfied throws InvalidOperationException, which
			// is caught below to decline the promotion rather than fail the dispatch.
			var resolved = provider.GetService(handlerType) ?? ActivatorUtilities.CreateInstance(provider, handlerType);

			if (resolved is null)
			{
				handler = default!;
				return false;
			}

			_ = _singletonNoContextHandlerCache.TryAdd(handlerType, resolved);
			handler = resolved;
			return true;
		}
		catch (InvalidOperationException)
		{
			handler = default!;
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool CanPromoteTransientStatelessNoContextHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type handlerType)
	{
		if (_parameterlessStatelessPromotionEligibilityCache.TryGetValue(handlerType, out var cached))
		{
			return cached;
		}

		// Split the shape test from the lifetime test so a handler that is stateless but registered with a
		// more expensive lifetime can be told apart from one that simply is not promotable. Only the first
		// deserves the advisory below.
		// Deliberately NOT gated on IsSelfRegisteredHandler. That check asked whether the container can
		// resolve the concrete handler type, which is true only when the consumer ALSO wrote
		// AddTransient<THandler>() alongside the interface mapping -- an extra registration no
		// documentation asks for. Gating the shared instance on it made the leaner dispatch path reachable
		// only by accident, and left the documented registration paying for a fresh handler on every
		// dispatch. The properties that actually make sharing safe are the three below, and none of them
		// depends on how the handler was registered; the lifetime check that follows is what keeps a
		// Scoped or Singleton registration honoured.
		var shapeAllowsSharing = !HandlerActivator.RequiresContextInjection(handlerType) &&
								 HasParameterlessConstructor(handlerType) &&
								 !HasInstanceFields(handlerType);

		var eligible = shapeAllowsSharing && _scopeResolver.MayPromoteToSharedInstance(handlerType);

		// Advisory, emitted here because this block runs once per handler type — the cache check above
		// returns before it on every later dispatch. It must never move onto the per-dispatch path.
		if (shapeAllowsSharing &&
			!eligible &&
			_scopeResolver.TryGetRegisteredLifetime(handlerType, out var registeredLifetime) &&
			registeredLifetime != ServiceLifetime.Transient)
		{
			LogHandlerLifetimeMoreExpensiveThanNeeded(handlerType.Name, registeredLifetime.ToString());
		}

		_ = _parameterlessStatelessPromotionEligibilityCache.TryAdd(handlerType, eligible);
		return eligible;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool HasParameterlessConstructor(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
	{
		var constructors = handlerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
		for (var i = 0; i < constructors.Length; i++)
		{
			if (constructors[i].GetParameters().Length == 0)
			{
				return true;
			}
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes' in call to 'System.Type.GetFields'",
		Justification =
			"A shape probe whose only effect is to decline an optimisation. Fields trimming removed cannot be "
			+ "read, and their absence makes the handler look stateless, so the worst outcome is that a handler "
			+ "is promoted to a shared instance -- which is only reached when the scope resolver separately "
			+ "agrees it may be shared. Requiring the fields be preserved would push a trim contract through "
			+ "every caller in the handler-type flow to keep a heuristic exact.")]
	private static bool HasInstanceFields(Type handlerType)
	{
		var fields = handlerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (var i = 0; i < fields.Length; i++)
		{
			if (!fields[i].IsStatic)
			{
				return true;
			}
		}

		return false;
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

		if (_concreteRegistry is not null)
		{
			if (!_concreteRegistry.TryGetHandler(messageType, out var concreteEntry))
			{
				entry = default!;
				return false;
			}

			entry = concreteEntry;
			_ = _handlerEntryCache.TryAdd(messageType, concreteEntry);
			return true;
		}

		if (!registry.TryGetHandler(messageType, out var ifaceEntry) || ifaceEntry is not HandlerRegistryEntry resolved)
		{
			entry = default!;
			return false;
		}

		entry = resolved;
		_ = _handlerEntryCache.TryAdd(messageType, resolved);
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
		var concreteEntries = GetConcreteEntries(registry);
		if (concreteEntries.Count == 0)
		{
			return FrozenDictionary<Type, HandlerRegistryEntry>.Empty;
		}

		var map = new Dictionary<Type, HandlerRegistryEntry>(concreteEntries.Count);
		for (var index = 0; index < concreteEntries.Count; index++)
		{
			var entry = concreteEntries[index];
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
		var concreteEntries = GetConcreteEntries(registry);
		if (concreteEntries.Count == 0)
		{
			return FrozenDictionary<Type, HandlerRegistryEntry[]>.Empty;
		}

		var grouped = new Dictionary<Type, List<HandlerRegistryEntry>>();
		for (var index = 0; index < concreteEntries.Count; index++)
		{
			var entry = concreteEntries[index];
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

	private static FrozenDictionary<Type, EventDispatchPlan[]> InitializeFrozenEventDispatchPlanMap(IHandlerRegistry registry, ILogger logger)
	{
		var eventHandlers = InitializeFrozenEventHandlersMap(registry);
		if (eventHandlers.Count == 0)
		{
			return FrozenDictionary<Type, EventDispatchPlan[]>.Empty;
		}

		var plans = new Dictionary<Type, EventDispatchPlan[]>(eventHandlers.Count);
		foreach (var pair in eventHandlers)
		{
			plans[pair.Key] = CreateEventDispatchPlans(pair.Value, logger);
		}

		return plans.ToFrozenDictionary();
	}

	private static FrozenDictionary<Type, DirectActionDispatchPlan> InitializeFrozenDirectActionPlanMap(IHandlerRegistry registry, ILogger logger)
	{
		var entries = GetConcreteEntries(registry);
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

			plans.Add(entry.MessageType, CreateRuntimeDirectActionDispatchPlan(entry, logger));
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

	private static ConcurrentDictionary<Type, DirectActionDispatchPlan?> InitializeDirectActionPlanCache(IHandlerRegistry registry, ILogger logger)
	{
		var cache = new ConcurrentDictionary<Type, DirectActionDispatchPlan?>();
		var entries = GetConcreteEntries(registry);
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

			_ = cache.TryAdd(entry.MessageType, CreateRuntimeDirectActionDispatchPlan(entry, logger));
		}

		return cache;
	}

	/// <summary>
	/// Returns the concrete-typed entry list from the registry, avoiding interface casts.
	/// Uses <see cref="HandlerRegistry.GetAll"/> directly when the registry is the default implementation.
	/// </summary>
	private static IReadOnlyList<HandlerRegistryEntry> GetConcreteEntries(IHandlerRegistry registry)
	{
		if (registry is HandlerRegistry concrete)
		{
			return concrete.GetAll();
		}

		// Fallback: interface path — entries must be HandlerRegistryEntry instances.
		var iface = registry.GetAll();
		var result = new List<HandlerRegistryEntry>(iface.Count);
		for (var i = 0; i < iface.Count; i++)
		{
			result.Add((HandlerRegistryEntry)iface[i]);
		}

		return result;
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
		// CA1849/RS0030: Safe synchronous access — only called when task.IsCompletedSuccessfully
		// is true (see callers), so .Result never blocks or throws AggregateException.
#pragma warning disable CA1849, RS0030
		return task.Result;
#pragma warning restore CA1849, RS0030
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

	[UnconditionalSuppressMessage("Trimming", "IL2069",
		Justification = "Handler types are registered at startup and preserved by the DI container.")]
	private readonly record struct DirectActionDispatchPlan(
		// param and field as well as property. Annotating only the generated property leaves the
		// constructor parameter and its backing field unannotated, so the caller's annotated Type is
		// stored somewhere trimming believes carries no requirement -- which is what IL2069 reports.
		[param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
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
		Func<IDispatchAction, IServiceProvider, IMessageContext?, CancellationToken, ValueTask<object?>> Invoke);

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

	[UnconditionalSuppressMessage("Trimming", "IL2069",
		Justification = "Handler types are registered at startup and preserved by the DI container.")]
	private readonly record struct EventDispatchPlan(
		// param and field as well as property. Annotating only the generated property leaves the
		// constructor parameter and its backing field unannotated, so the caller's annotated Type is
		// stored somewhere trimming believes carries no requirement -- which is what IL2069 reports.
		[param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		[field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
		Type HandlerType,
		bool RequiresContext,
		EventHandlerAsyncInvoker Invoke);

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.NoHandlersForEvent, LogLevel.Warning,
		"No handlers registered for event {EventType}")]
	private partial void LogNoHandlersRegisteredForEvent(string eventType);

	// Information rather than Warning: an explicit Scoped or Singleton registration is a legitimate choice
	// and is honoured, so warning on it would train consumers to filter this category — and then the
	// messages that do matter get filtered with it. The wording says what we DO (honour the registration)
	// before what they COULD do, because the previous behaviour silently overrode them and a consumer
	// reading this needs to know that is no longer the case.
	[LoggerMessage(DeliveryEventId.HandlerLifetimeMoreExpensiveThanNeeded, LogLevel.Information,
		"Handler {HandlerType} is registered {Lifetime} and is being resolved that way. It has no " +
		"constructor dependencies and no instance state, so registering it Transient would let Dispatch " +
		"reuse a single instance and skip a per-dispatch activation. Registering it Transient is optional; " +
		"if the {Lifetime} lifetime is deliberate, nothing needs to change.")]
	private partial void LogHandlerLifetimeMoreExpensiveThanNeeded(string handlerType, string lifetime);

	// Debug rather than Warning: the fallback dispatches correctly, it is only slower. But it must
	// leave a thread to pull -- a consumer whose throughput silently halved otherwise has nothing to read.
	[LoggerMessage(DeliveryEventId.TypedInvokerBuildFailed, LogLevel.Debug,
		"Could not build a typed dispatch invoker for handler {HandlerType}; dispatch falls back to the reflection-based path.")]
	private static partial void LogTypedInvokerBuildFailed(ILogger logger, Exception exception, string handlerType);
}
