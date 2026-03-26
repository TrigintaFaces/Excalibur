// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Internal implementation of <see cref="IProjectionRecovery"/> that reloads
/// events from the event store and re-applies them through registered handlers.
/// </summary>
internal sealed class ProjectionRecoveryService : IProjectionRecovery
{
	private readonly IProjectionRegistry _registry;
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<ProjectionRecoveryService> _logger;

	public ProjectionRecoveryService(
		IProjectionRegistry registry,
		IEventStore eventStore,
		IEventSerializer eventSerializer,
		IServiceProvider serviceProvider,
		ILogger<ProjectionRecoveryService> logger)
	{
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentNullException.ThrowIfNull(eventSerializer);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_registry = registry;
		_eventStore = eventStore;
		_eventSerializer = eventSerializer;
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task ReapplyAsync<TProjection>(
		string aggregateId,
		CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);

		var registration = _registry.GetRegistration(typeof(TProjection))
			?? throw new InvalidOperationException(
				$"No projection registration found for type '{typeof(TProjection).Name}'. " +
				"Ensure the projection is registered via AddProjection<T>().");

		var projection = (MultiStreamProjection<TProjection>)registration.Projection;
		var store = _serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();

		// Load all events for this aggregate
		// We need the aggregate type from the registration -- for now, use the aggregate ID
		// The caller knows the aggregate type context from the InlineProjectionException
		var storedEvents = await _eventStore.LoadAsync(
			aggregateId,
			string.Empty, // aggregate type is not stored in registration; events are loaded by ID
			cancellationToken).ConfigureAwait(false);

		// Create fresh projection state
		var state = new TProjection();

		// Deserialize and apply all events through the same handlers
		foreach (var storedEvent in storedEvents)
		{
			var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
			var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);
			projection.Apply(state, domainEvent);
		}

		// Persist the recovered projection
		await store.UpsertAsync(aggregateId, state, cancellationToken)
			.ConfigureAwait(false);

		_logger.LogInformation(
			"Successfully recovered projection '{ProjectionType}' for aggregate '{AggregateId}' " +
			"by replaying {EventCount} events.",
			typeof(TProjection).Name,
			aggregateId,
			storedEvents.Count);
	}
}
