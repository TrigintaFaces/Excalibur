// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

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
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection recovery requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	public async Task ReapplyAsync<TProjection>(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);

		var registration = _registry.GetRegistration(typeof(TProjection))
			?? throw new InvalidOperationException(
				$"No projection registration found for type '{typeof(TProjection).Name}'. " +
				"Ensure the projection is registered via AddProjection<T>().");

		var projection = (MultiStreamProjection<TProjection>)registration.Projection;
		var store = _serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();

		// Load all events for this aggregate using the caller-provided aggregate type
		var storedEvents = await _eventStore.LoadAsync(
			aggregateId,
			aggregateType,
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
