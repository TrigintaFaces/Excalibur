// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default implementation of <see cref="IEventNotificationBroker"/> that orchestrates
/// inline projections and notification handlers after events are committed.
/// </summary>
/// <remarks>
/// <para>
/// Execution order (R27.8 + R27.20):
/// <list type="number">
/// <item>Phase 1: ALL inline projections via <c>Task.WhenAll</c> (concurrent across types,
/// sequential events within each type).</item>
/// <item>Phase 2: ALL notification handlers sequentially (after ALL projections complete).</item>
/// </list>
/// </para>
/// <para>Projections and handlers NEVER overlap.</para>
/// </remarks>
internal sealed class EventNotificationBroker : IEventNotificationBroker
{
	// Cache MakeGenericType + GetMethod results to avoid repeated reflection per notification.
	// Bounded to prevent unbounded growth from event type proliferation.
	private static readonly ConcurrentDictionary<Type, (Type InterfaceType, System.Reflection.MethodInfo Method)> HandlerMethodCache = new();
	private const int MaxCacheSize = 1024;

	private readonly InlineProjectionProcessor _processor;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<EventNotificationOptions> _options;
	private readonly ILogger<EventNotificationBroker> _logger;

	public EventNotificationBroker(
		InlineProjectionProcessor processor,
		IServiceScopeFactory scopeFactory,
		IOptions<EventNotificationOptions> options,
		ILogger<EventNotificationBroker> logger,
		IEnumerable<EventNotificationServiceCollectionExtensions.IConfigureProjection> projectionConfigurations)
	{
		ArgumentNullException.ThrowIfNull(processor);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_processor = processor;
		_scopeFactory = scopeFactory;
		_options = options;
		_logger = logger;

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
		EventNotificationContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentNullException.ThrowIfNull(context);

		if (events.Count == 0)
		{
			return;
		}

		var opts = _options.Value;

		// Phase 1: Inline projections (concurrent across projection types)
		await _processor.ProcessAsync(
				events, context, opts.FailurePolicy, cancellationToken)
			.ConfigureAwait(false);

		// Phase 2: Notification handlers (sequential, after ALL projections complete -- R27.8)
		// Respects the same FailurePolicy as projections for consistency.
		await InvokeNotificationHandlersAsync(events, context, opts.FailurePolicy, cancellationToken)
			.ConfigureAwait(false);
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
			var eventType = @event.GetType();
			var (handlerInterfaceType, method) = ResolveHandlerMethod(eventType);

			// Resolve all handlers for this event type
			var handlers = scope.ServiceProvider.GetServices(handlerInterfaceType);
			foreach (var handler in handlers)
			{
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
