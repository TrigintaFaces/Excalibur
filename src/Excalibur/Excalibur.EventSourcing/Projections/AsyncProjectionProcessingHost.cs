// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Background service that processes all <see cref="ProjectionMode.Async"/> projections
/// by polling the global event stream and dispatching events to registered projection handlers.
/// </summary>
/// <remarks>
/// <para>
/// This host is the async counterpart to <see cref="InlineProjectionProcessor"/>. While inline
/// projections run synchronously during <c>SaveAsync</c>, this host polls the global stream
/// independently, enabling eventual consistency for read models.
/// </para>
/// <para>
/// Register via <c>es.EnableProjectionProcessing()</c> on <see cref="DependencyInjection.IEventSourcingBuilder"/>.
/// Requires an <see cref="IGlobalStreamQuery"/> implementation (e.g., from <c>UseSqlServer()</c>).
/// </para>
/// </remarks>
internal sealed partial class AsyncProjectionProcessingHost : BackgroundService
{
	private readonly IProjectionRegistry _registry;
	private readonly IEventSerializer _eventSerializer;
	private readonly ISubscriptionCheckpointStore _checkpointStore;
	private readonly IOptions<GlobalStreamProjectionOptions> _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AsyncProjectionProcessingHost> _logger;
	private readonly ProjectionObservability? _observability;
	private readonly ProjectionHealthState? _healthState;

	private GlobalStreamPosition _currentPosition = GlobalStreamPosition.Start;
	private long _eventsSinceCheckpoint;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncProjectionProcessingHost"/> class.
	/// </summary>
	public AsyncProjectionProcessingHost(
		IProjectionRegistry registry,
		IEventSerializer eventSerializer,
		ISubscriptionCheckpointStore checkpointStore,
		IOptions<GlobalStreamProjectionOptions> options,
		IServiceProvider serviceProvider,
		ILogger<AsyncProjectionProcessingHost> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_observability = serviceProvider.GetService(typeof(ProjectionObservability)) as ProjectionObservability;
		_healthState = serviceProvider.GetService(typeof(ProjectionHealthState)) as ProjectionHealthState;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Resolve IGlobalStreamQuery from DI — it's provider-specific (e.g., SqlServer).
		if (_serviceProvider.GetService(typeof(IGlobalStreamQuery)) is not IGlobalStreamQuery globalStreamQuery)
		{
			LogNoGlobalStreamQuery();
			return;
		}

		var asyncRegistrations = _registry.GetByMode(ProjectionMode.Async);
		if (asyncRegistrations.Count == 0)
		{
			return;
		}

		var opts = _options.Value;
		var checkpointName = opts.ProjectionName;

		// Restore checkpoint from last run
		var lastCheckpoint = await _checkpointStore.GetCheckpointAsync(checkpointName, stoppingToken)
			.ConfigureAwait(false);
		if (lastCheckpoint.HasValue)
		{
			_currentPosition = new GlobalStreamPosition(lastCheckpoint.Value, DateTimeOffset.MinValue);
		}

		LogAsyncProjectionHostStarted(asyncRegistrations.Count);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var events = await globalStreamQuery.ReadAllAsync(
					_currentPosition,
					opts.BatchSize,
					stoppingToken).ConfigureAwait(false);

				if (events.Count == 0)
				{
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
					continue;
				}

				// Group stored events by aggregate so each apply delegate gets
				// a coherent batch with the correct EventNotificationContext.
				var groups = GroupByAggregate(events);

				foreach (var group in groups)
				{
					var domainEvents = DeserializeEvents(group.StoredEvents);
					if (domainEvents.Count == 0)
					{
						continue;
					}

					var lastEvent = group.StoredEvents[group.StoredEvents.Count - 1];
					var context = new EventNotificationContext(
						group.AggregateId,
						group.AggregateType,
						lastEvent.Version,
						lastEvent.Timestamp);

					// Dispatch to all async projection registrations concurrently
					await DispatchToProjectionsAsync(
						asyncRegistrations, domainEvents, context, stoppingToken).ConfigureAwait(false);
				}

				// Advance position past the last event in the batch
				var lastStoredEvent = events[events.Count - 1];
				_currentPosition = new GlobalStreamPosition(
					lastStoredEvent.Version + 1,
					lastStoredEvent.Timestamp);
				_eventsSinceCheckpoint += events.Count;

				// Checkpoint when threshold reached
				if (_eventsSinceCheckpoint >= opts.CheckpointInterval)
				{
					await _checkpointStore.StoreCheckpointAsync(
						checkpointName, _currentPosition.Position, stoppingToken).ConfigureAwait(false);
					LogAsyncProjectionCheckpointSaved(_currentPosition.Position);
					_eventsSinceCheckpoint = 0;
				}

				LogAsyncProjectionBatchProcessed(events.Count, _currentPosition.Position);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
#pragma warning disable CA1031 // Catch general exceptions -- resilient polling loop
			catch (Exception ex)
#pragma warning restore CA1031
			{
				LogAsyncProjectionHostError(ex);
				await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}

		// Persist final checkpoint on graceful shutdown
		if (_eventsSinceCheckpoint > 0)
		{
			try
			{
				await _checkpointStore.StoreCheckpointAsync(
					checkpointName, _currentPosition.Position, CancellationToken.None).ConfigureAwait(false);
				LogAsyncProjectionCheckpointSaved(_currentPosition.Position);
			}
#pragma warning disable CA1031 // Catch general exceptions -- shutdown must not throw
			catch (Exception ex)
#pragma warning restore CA1031
			{
				LogAsyncProjectionHostError(ex);
			}
		}

		LogAsyncProjectionHostStopped();
	}

	/// <summary>
	/// Groups stored events by (AggregateId, AggregateType) to create coherent
	/// batches for projection apply delegates.
	/// </summary>
	private static List<AggregateEventGroup> GroupByAggregate(IReadOnlyList<StoredEvent> events)
	{
		var groups = new Dictionary<string, AggregateEventGroup>(StringComparer.Ordinal);

		foreach (var e in events)
		{
			var key = string.Concat(e.AggregateType, ":", e.AggregateId);
			if (!groups.TryGetValue(key, out var group))
			{
				group = new AggregateEventGroup(e.AggregateId, e.AggregateType);
				groups[key] = group;
			}

			group.StoredEvents.Add(e);
		}

		return [.. groups.Values];
	}

	/// <summary>
	/// Deserializes stored events to domain events, skipping events that fail deserialization.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection host requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	private List<IDomainEvent> DeserializeEvents(List<StoredEvent> storedEvents)
	{
		var domainEvents = new List<IDomainEvent>(storedEvents.Count);

		foreach (var storedEvent in storedEvents)
		{
			try
			{
				var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
				var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

				if (domainEvent is not null)
				{
					domainEvents.Add(domainEvent);
				}
			}
#pragma warning disable CA1031 // Catch general exceptions -- skip undeserializable events
			catch (Exception ex)
#pragma warning restore CA1031
			{
				LogAsyncProjectionEventError(storedEvent.EventId, ex);
			}
		}

		return domainEvents;
	}

	/// <summary>
	/// Dispatches domain events to all async projection registrations concurrently.
	/// </summary>
	private async Task DispatchToProjectionsAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		List<IDomainEvent> domainEvents,
		EventNotificationContext context,
		CancellationToken cancellationToken)
	{
		var tasks = new Task[registrations.Count];

		for (var i = 0; i < registrations.Count; i++)
		{
			var registration = registrations[i];

			if (registration.InlineApply is not null)
			{
				tasks[i] = registration.InlineApply(
					domainEvents, context, _serviceProvider, cancellationToken);
			}
			else
			{
				tasks[i] = Task.CompletedTask;
			}
		}

		for (var j = 0; j < tasks.Length; j++)
		{
			try
			{
				await tasks[j].ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Catch general exceptions -- partial failure; log and continue
			catch (Exception ex)
#pragma warning restore CA1031
			{
				var projectionType = registrations[j].ProjectionType.Name;
				LogAsyncProjectionEventError(projectionType, ex);

				try
				{
					_healthState?.RecordInlineError(projectionType);
					_observability?.RecordError(projectionType, ex.GetType().Name);
				}
				catch
				{
					// Swallow -- metrics must not affect projection pipeline
				}
			}
		}
	}

	/// <summary>
	/// Groups stored events belonging to a single aggregate.
	/// </summary>
	private sealed class AggregateEventGroup(string aggregateId, string aggregateType)
	{
		public string AggregateId { get; } = aggregateId;
		public string AggregateType { get; } = aggregateType;
		public List<StoredEvent> StoredEvents { get; } = [];
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStarted, LogLevel.Information,
		"Async projection processing host started with {ProjectionCount} async projection(s).")]
	private partial void LogAsyncProjectionHostStarted(int projectionCount);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostStopped, LogLevel.Information,
		"Async projection processing host stopped.")]
	private partial void LogAsyncProjectionHostStopped();

	[LoggerMessage(EventSourcingEventId.AsyncProjectionBatchProcessed, LogLevel.Debug,
		"Async projections processed batch of {EventCount} events, position now at {Position}.")]
	private partial void LogAsyncProjectionBatchProcessed(int eventCount, long position);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointSaved, LogLevel.Debug,
		"Async projection checkpoint saved at position {Position}.")]
	private partial void LogAsyncProjectionCheckpointSaved(long position);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionEventError, LogLevel.Error,
		"Error processing event {EventId} in async projection host.")]
	private partial void LogAsyncProjectionEventError(string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostError, LogLevel.Error,
		"Async projection processing host encountered an error.")]
	private partial void LogAsyncProjectionHostError(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionNoGlobalStreamQuery, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Async projection processing cannot start. " +
		"Ensure your event store provider (e.g., UseSqlServer) registers IGlobalStreamQuery.")]
	private partial void LogNoGlobalStreamQuery();

	#endregion Logging
}
