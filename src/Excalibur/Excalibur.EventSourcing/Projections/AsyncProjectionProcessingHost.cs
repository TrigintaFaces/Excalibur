// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
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

				// Deserialize the batch in GLOBAL order, HALTING at the first poison event (a deserialize
				// failure or a null deserialization). A poison event is recorded and marks the host
				// unhealthy; it is NEVER skipped or checkpointed past — it is left for the next read so the
				// read model can never silently drift from the event log. A transient failure self-heals on
				// the next poll; a permanent one keeps the host unhealthy until an operator acts. This
				// mirrors GlobalStreamProjectionHost (c3jdco / ADR-336 Amendment 3a / FR-8).
				var deserialized = new List<DeserializedEvent>(events.Count);
				var poisonEncountered = false;

				foreach (var storedEvent in events)
				{
					stoppingToken.ThrowIfCancellationRequested();

					IDomainEvent domainEvent;
					try
					{
						domainEvent = DeserializeOrThrow(storedEvent);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						// Poison event: record + mark unhealthy, then HALT this batch at the failed event.
						LogAsyncProjectionEventError(storedEvent.EventId, ex);
						RecordPoison(checkpointName, ex);
						poisonEncountered = true;
						break;
					}

					deserialized.Add(new DeserializedEvent(storedEvent, domainEvent));
				}

				// Dispatch only the good prefix (events before any poison), grouped by aggregate so each
				// apply delegate receives a coherent batch with the correct EventNotificationContext.
				if (deserialized.Count > 0)
				{
					foreach (var group in GroupByAggregate(deserialized))
					{
						var context = new EventNotificationContext(
							group.AggregateId,
							group.AggregateType,
							group.LastVersion,
							group.LastTimestamp);

						await DispatchToProjectionsAsync(
							asyncRegistrations, group.DomainEvents, context, stoppingToken).ConfigureAwait(false);
					}

					// Advance ONLY to the last good-prefix event's GLOBAL ordinal (GlobalPosition), never the
					// per-aggregate Version. The poison event (and everything after it) stays unread/unskipped.
					var lastGood = deserialized[deserialized.Count - 1].Stored;
					_currentPosition = new GlobalStreamPosition(lastGood.GlobalPosition + 1, lastGood.Timestamp);
					_eventsSinceCheckpoint += deserialized.Count;

					// Checkpoint when threshold reached (only ever the last-good position; never past a poison event).
					if (_eventsSinceCheckpoint >= opts.CheckpointInterval)
					{
						await _checkpointStore.StoreCheckpointAsync(
							checkpointName, _currentPosition.Position, stoppingToken).ConfigureAwait(false);
						LogAsyncProjectionCheckpointSaved(_currentPosition.Position);
						_eventsSinceCheckpoint = 0;
					}

					LogAsyncProjectionBatchProcessed(deserialized.Count, _currentPosition.Position);
				}

				// On a poison event, back off before re-reading so we don't tight-loop on a permanent
				// failure; the next read resumes from the unadvanced checkpoint (reprocess, not skip).
				if (poisonEncountered)
				{
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
				}
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
	/// Resolves and deserializes a stored event to a domain event, throwing on a null result rather
	/// than silently dropping it (a null deserialization is a poison event, not an empty batch).
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection host requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	private IDomainEvent DeserializeOrThrow(StoredEvent storedEvent)
	{
		var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
		return _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType)
			?? throw new InvalidOperationException(
				$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null; refusing to skip it.");
	}

	/// <summary>
	/// Records a poison event against observability and host health, swallowing telemetry failures so
	/// they never affect the projection pipeline.
	/// </summary>
	private void RecordPoison(string projectionName, Exception ex)
	{
		try
		{
			_observability?.RecordError(projectionName, ex.GetType().Name);
		}
		catch
		{
			// Swallow -- metrics must not affect the projection pipeline.
		}

		try
		{
			_healthState?.RecordInlineError(projectionName);
		}
		catch
		{
			// Swallow -- health recording must not affect the projection pipeline.
		}
	}

	/// <summary>
	/// Groups deserialized events by (AggregateId, AggregateType) to create coherent
	/// batches for projection apply delegates, preserving global order within each aggregate.
	/// </summary>
	private static List<AggregateEventGroup> GroupByAggregate(List<DeserializedEvent> events)
	{
		var groups = new Dictionary<string, AggregateEventGroup>(StringComparer.Ordinal);

		foreach (var e in events)
		{
			var stored = e.Stored;
			var key = string.Concat(stored.AggregateType, ":", stored.AggregateId);
			if (!groups.TryGetValue(key, out var group))
			{
				group = new AggregateEventGroup(stored.AggregateId, stored.AggregateType);
				groups[key] = group;
			}

			group.DomainEvents.Add(e.Domain);

			// Global order is ascending, so the last event seen for an aggregate carries its latest
			// version/timestamp for the notification context.
			group.LastVersion = stored.Version;
			group.LastTimestamp = stored.Timestamp;
		}

		return [.. groups.Values];
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
				// IProjectionStore<T> is scoped; this host is a singleton BackgroundService, so the
				// apply delegate must receive a provider from a created scope, not the captured root
				// provider (which throws under DI scope validation). A scope per projection also
				// isolates scoped state across the concurrently-applied projections.
				tasks[i] = ApplyInScopeAsync(registration, domainEvents, context, cancellationToken);
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
				var projectionName = registrations[j].ProjectionType.Name;
				LogAsyncProjectionDispatchError(projectionName, ex);

				try
				{
					_healthState?.RecordInlineError(projectionName);
					_observability?.RecordError(projectionName, ex.GetType().Name);
				}
				catch
				{
					// Swallow -- metrics must not affect projection pipeline
				}
			}
		}
	}

	private async Task ApplyInScopeAsync(
		ProjectionRegistration registration,
		List<IDomainEvent> domainEvents,
		EventNotificationContext context,
		CancellationToken cancellationToken)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();
		await registration.InlineApply!(domainEvents, context, scope.ServiceProvider, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// A stored event paired with its successfully-deserialized domain event.
	/// </summary>
	private readonly record struct DeserializedEvent(StoredEvent Stored, IDomainEvent Domain);

	/// <summary>
	/// Groups deserialized events belonging to a single aggregate.
	/// </summary>
	private sealed class AggregateEventGroup(string aggregateId, string aggregateType)
	{
		public string AggregateId { get; } = aggregateId;
		public string AggregateType { get; } = aggregateType;
		public List<IDomainEvent> DomainEvents { get; } = [];
		public long LastVersion { get; set; }
		public DateTimeOffset LastTimestamp { get; set; }
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
		"Poison event {EventId} halted the async projection host; checkpoint not advanced past it.")]
	private partial void LogAsyncProjectionEventError(string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionDispatchError, LogLevel.Error,
		"Error dispatching events to async projection {ProjectionName}.")]
	private partial void LogAsyncProjectionDispatchError(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostError, LogLevel.Error,
		"Async projection processing host encountered an error.")]
	private partial void LogAsyncProjectionHostError(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionNoGlobalStreamQuery, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Async projection processing cannot start. " +
		"Ensure your event store provider (e.g., UseSqlServer) registers IGlobalStreamQuery.")]
	private partial void LogNoGlobalStreamQuery();

	#endregion Logging
}
