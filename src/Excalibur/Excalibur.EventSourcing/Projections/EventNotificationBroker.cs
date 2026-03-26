// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
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
	private readonly InlineProjectionProcessor _processor;
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<EventNotificationOptions> _options;
	private readonly ILogger<EventNotificationBroker> _logger;

	public EventNotificationBroker(
		InlineProjectionProcessor processor,
		IServiceProvider serviceProvider,
		IOptions<EventNotificationOptions> options,
		ILogger<EventNotificationBroker> logger,
		IEnumerable<global::Microsoft.Extensions.DependencyInjection.EventNotificationServiceCollectionExtensions.IConfigureProjection> projectionConfigurations)
	{
		ArgumentNullException.ThrowIfNull(processor);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_processor = processor;
		_serviceProvider = serviceProvider;
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
		await InvokeNotificationHandlersAsync(events, context, cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task InvokeNotificationHandlersAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		CancellationToken cancellationToken)
	{
		foreach (var @event in events)
		{
			var eventType = @event.GetType();
			var handlerInterfaceType = typeof(IEventNotificationHandler<>).MakeGenericType(eventType);

			// Resolve all handlers for this event type
			var handlers = _serviceProvider.GetServices(handlerInterfaceType);
			foreach (var handler in handlers)
			{
				if (handler is null)
				{
					continue;
				}

				try
				{
					// Invoke HandleAsync via the interface method
					var method = handlerInterfaceType.GetMethod("HandleAsync")!;
					var task = (Task)method.Invoke(handler, [@event, context, cancellationToken])!;
					await task.ConfigureAwait(false);
				}
#pragma warning disable CA1031 // Catch general exceptions -- handlers should not crash the notification pipeline
				catch (Exception ex)
#pragma warning restore CA1031
				{
					_logger.LogError(
						ex,
						"Event notification handler '{HandlerType}' failed for event '{EventType}' " +
						"on aggregate {AggregateType}/{AggregateId}.",
						handler.GetType().Name,
						eventType.Name,
						context.AggregateType,
						context.AggregateId);
				}
			}
		}
	}
}
