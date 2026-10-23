// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default implementation of <see cref="IEventNotificationBroker"/> that invokes the notification
/// handlers after events are committed.
/// </summary>
/// <remarks>
/// <para>
/// Handlers run sequentially, one event at a time in commit order, resolved from a fresh DI scope,
/// under a bound this broker mints rather than the caller's cancellation token.
/// </para>
/// <para>No projection is written here — see the note in <c>NotifyAsync</c> for why.</para>
/// </remarks>
internal sealed class EventNotificationBroker : IEventNotificationBroker
{
	// Cache MakeGenericType + GetMethod results to avoid repeated reflection per notification.
	// Bounded to prevent unbounded growth from event type proliferation.
	private static readonly ConcurrentDictionary<Type, (Type InterfaceType, System.Reflection.MethodInfo Method)> HandlerMethodCache = new();
	private const int MaxCacheSize = 1024;

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<EventNotificationOptions> _options;
	private readonly ILogger<EventNotificationBroker> _logger;
	private readonly TimeProvider _timeProvider;

	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public EventNotificationBroker(
		IServiceScopeFactory scopeFactory,
		IOptions<EventNotificationOptions> options,
		ILogger<EventNotificationBroker> logger,
		IEnumerable<EventNotificationServiceCollectionExtensions.IConfigureProjection> projectionConfigurations,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_scopeFactory = scopeFactory;
		_options = options;
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;

		// Eagerly invoke all deferred projection configurations (B2 fix).
		// IConfigureProjection instances are registered by AddProjection<T>() but
		// need to be resolved and invoked to populate the IProjectionRegistry.
		foreach (var config in projectionConfigurations)
		{
			config.Configure();
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MakeGenericType is used to resolve event notification handlers. Register handlers explicitly for AOT scenarios.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Handler types are preserved through DI registration.")]
	public async Task NotifyAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentNullException.ThrowIfNull(context);

		if (events.Count == 0)
		{
			return;
		}

		var opts = _options.Value;

		// THE TOKEN IS MINTED HERE, AND IT IS NOT THE CALLER'S. Every consumer of this phase -- inline
		// projections and notification handlers alike -- routes through this one method, so the detach
		// lives here and no call site gets to decide. The caller's token was threaded straight through
		// until now, which meant a shut-down host or an abandoned request could cancel work whose
		// subject was already durable: the save succeeded, the read model did not get it, and the
		// caller was told the save failed.
		//
		// A bound rather than CancellationToken.None, because an unresponsive store must not be able to
		// hold a save open forever. It is a backstop; see PostCommitTimeout for why it is not a budget.
		using var bound = new CancellationTokenSource(opts.PostCommitTimeout, _timeProvider);

		// PROJECTIONS ARE NO LONGER WRITTEN HERE, and that is the point.
		//
		// This phase used to fold "inline" projections in-process, after the events were durable, as a
		// SECOND uncoordinated writer. Two concurrent saves then read the same row, both folded, and the
		// last write won -- a lost fold that returned success to both callers. The write carried no
		// global position either, so the row could not be advanced by anything afterwards and only a
		// full rebuild could repair it.
		//
		// A read model is a replica, and replicas are updated by replication rather than by the writer
		// dual-writing into them. Read-your-writes did not go away with the removal: SaveAsync returns a
		// session token and IProjectionSessionReader honours it.
		//
		// WHAT REMOVING THIS DOES **NOT** BUY -- stated because the obvious claim is wrong, and was
		// written here before it was checked. The projection host is NOT the only writer afterwards.
		// ProjectionRebuildService (via ProjectionReplayFold), ProjectionRecoveryService, a second
		// GlobalStreamProjectionHost (which performs no leader election at all) and any consumer holding
		// the public IProjectionStore<T> can all write a row the host writes.
		//
		// What actually closes the race is the POSITION, not the writer count. On a store implementing
		// IPositionedProjectionStore<T> the conditional write refuses the loser and the refusal is
		// visible -- ProjectionReplayFold.cs:276 calls that CAS "the only thing that surfaces a live
		// writer racing the rebuild". On a store WITHOUT that capability, ProjectionReplayFold.cs:243
		// and :252 upsert unconditionally, and a rebuild can still silently regress a row the host has
		// advanced past. So the honest guarantee is: exactly-once fold holds where the store is
		// positioned, and nowhere else. Closing that is tracked separately; it predates this change and
		// is not introduced by it.
		//
		// Notification handlers remain: they are consumer callbacks, not a second writer of our state.
		var notifications = InvokeNotificationHandlersAsync(events, context, opts.FailurePolicy, bound.Token);
		try
		{
			await notifications.WaitAsync(bound.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (bound.IsCancellationRequested)
		{
			// Stop waiting even if an asynchronous handler ignores cancellation. The invocation owns its
			// scope until it really finishes; disposing it here would invalidate a running handler's
			// dependencies. Observe any eventual failure, and do not retry an effect already in flight.
			_ = ObserveTimedOutNotificationAsync(notifications);
			var timeout = new TimeoutException("The post-commit notification deadline expired. Events remain committed; a running handler may still complete.");
			_logger.LogError(timeout, "Post-commit notification timed out for aggregate {AggregateType}/{AggregateId}.",
				context.AggregateType, context.AggregateId);
			if (opts.FailurePolicy == NotificationFailurePolicy.Propagate)
			{
				throw timeout;
			}
		}
	}

	private async Task ObserveTimedOutNotificationAsync(Task notifications)
	{
		try
		{
			await notifications.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "A post-commit notification failed after its caller stopped waiting.");
		}
	}

	[RequiresDynamicCode("Uses Type.MakeGenericType to construct IEventNotificationHandler<TEvent> at runtime.")]
	[RequiresUnreferencedCode("Uses Type.GetMethod to dynamically invoke HandleAsync on resolved notification handlers.")]
	private async Task InvokeNotificationHandlersAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		NotificationFailurePolicy failurePolicy,
		CancellationToken cancellationToken)
	{
		// Notification handlers may be registered scoped; this broker is a singleton, so resolve
		// them from a fresh DI scope rather than the captured root provider (which throws under
		// scope validation: "Cannot resolve scoped service ... from root provider").
		await using var scope = _scopeFactory.CreateAsyncScope();

		foreach (var @event in events)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var eventType = @event.GetType();
			var (handlerInterfaceType, method) = ResolveHandlerMethod(eventType);

			// Resolve all handlers for this event type
			var handlers = scope.ServiceProvider.GetServices(handlerInterfaceType);
			foreach (var handler in handlers)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (handler is null)
				{
					continue;
				}

				try
				{
					var task = (Task)method.Invoke(handler, [@event, context, cancellationToken])!;
					await task.ConfigureAwait(false);
				}
				catch (TargetInvocationException tie) when (tie.InnerException is not null)
				{
					// Unwrap reflection wrapper to surface the real exception
					HandleNotificationError(tie.InnerException, handler, eventType, context, failurePolicy);
				}
#pragma warning disable CA1031 // Catch general exceptions -- handlers should not crash the notification pipeline
				catch (Exception ex) when (ex is not TargetInvocationException)
#pragma warning restore CA1031
				{
					HandleNotificationError(ex, handler, eventType, context, failurePolicy);
				}
			}
		}
	}

	[RequiresDynamicCode("Uses Type.MakeGenericType to construct IEventNotificationHandler<TEvent>.")]
	[RequiresUnreferencedCode("Uses Type.GetMethod to resolve HandleAsync on notification handlers.")]
	private static (Type InterfaceType, MethodInfo Method) ResolveHandlerMethod(Type eventType)
	{
		if (HandlerMethodCache.TryGetValue(eventType, out var cached))
		{
			return cached;
		}

		var interfaceType = typeof(IEventNotificationHandler<>).MakeGenericType(eventType);
		var method = interfaceType.GetMethod("HandleAsync")!;
		var entry = (interfaceType, method);

		// Bounded cache: skip caching when full to prevent unbounded memory growth
		if (HandlerMethodCache.Count < MaxCacheSize)
		{
			HandlerMethodCache.TryAdd(eventType, entry);
		}

		return entry;
	}

	private void HandleNotificationError(
		Exception ex,
		object handler,
		Type eventType,
		EventNotificationContext context,
		NotificationFailurePolicy failurePolicy)
	{
		_logger.LogError(
			ex,
			"Event notification handler '{HandlerType}' failed for event '{EventType}' " +
			"on aggregate {AggregateType}/{AggregateId}.",
			handler.GetType().Name,
			eventType.Name,
			context.AggregateType,
			context.AggregateId);

		if (failurePolicy == NotificationFailurePolicy.Propagate)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex);
		}
	}
}
