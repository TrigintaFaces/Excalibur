// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default implementation of <see cref="IProjectionRebuildService"/> that replays events
/// through projection handlers to rebuild projection state.
/// </summary>
/// <remarks>
/// <para>
/// This service tracks rebuild status per projection type using a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread safety.
/// It delegates event loading to the <see cref="Queries.IGlobalStreamQuery"/>
/// and projection application to registered <see cref="MultiStreamProjection{TProjection}"/> instances.
/// </para>
/// </remarks>
internal sealed partial class ProjectionRebuildService : IProjectionRebuildService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IEventSerializer _eventSerializer;
	private readonly IOptions<ProjectionRebuildOptions> _options;
	private readonly ILogger<ProjectionRebuildService> _logger;
	private readonly IUpcastingPipeline? _upcastingPipeline;
	private readonly bool _enableAutoUpcast;
	private readonly ConcurrentDictionary<string, ProjectionRebuildStatus> _statuses = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionRebuildService"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving projections and stores.</param>
	/// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
	/// <param name="options">The rebuild options.</param>
	/// <param name="logger">The logger.</param>
	public ProjectionRebuildService(
		IServiceProvider serviceProvider,
		IEventSerializer eventSerializer,
		IOptions<ProjectionRebuildOptions> options,
		ILogger<ProjectionRebuildService> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Apply the same upcasting the write side uses so a rebuilt read model does not diverge from the write
		// model on an evolved event schema. Optional: resolved from DI, never fails.
		_upcastingPipeline = serviceProvider.GetService(typeof(IUpcastingPipeline)) as IUpcastingPipeline;
		_enableAutoUpcast = (serviceProvider.GetService(typeof(IOptions<UpcastingOptions>)) as IOptions<UpcastingOptions>)
			?.Value.EnableAutoUpcastOnReplay ?? false;
	}

	/// <summary>
	/// Upcasts a deserialized event to its latest registered version when auto-upcast-on-replay is enabled, so a
	/// rebuild applies the same (current-schema) event the write-side aggregate would. Mirrors the repository.
	/// </summary>
	private IDomainEvent TryUpcastEvent(IDomainEvent domainEvent)
	{
		if (!_enableAutoUpcast || _upcastingPipeline is null || domainEvent is not IVersionedMessage)
		{
			return domainEvent;
		}

		return _upcastingPipeline.Upcast(domainEvent) as IDomainEvent ?? domainEvent;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Event deserialization during rebuild uses IEventSerializer which consumers configure with preserved types.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization during rebuild uses IEventSerializer which consumers configure.")]
	public async Task RebuildAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		var projectionName = typeof(TProjection).Name;

		LogRebuildStarted(projectionName);

		_statuses[projectionName] = new ProjectionRebuildStatus(
			projectionName,
			ProjectionRebuildState.Rebuilding,
			Progress: 0,
			LastRebuiltAt: null);

		try
		{
			// Resolve the global stream query for reading events
			if (_serviceProvider.GetService(typeof(Queries.IGlobalStreamQuery))
				is not Queries.IGlobalStreamQuery globalQuery)
			{
				LogNoGlobalQueryRegistered(projectionName);

				_statuses[projectionName] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

			// Resolve the multi-stream projection -- check DI first, then IProjectionRegistry (for inline projections)
			var projection = _serviceProvider.GetService(typeof(MultiStreamProjection<TProjection>))
				as MultiStreamProjection<TProjection>;

			if (projection is null
				&& _serviceProvider.GetService(typeof(IProjectionRegistry)) is IProjectionRegistry registry)
			{
				var registration = registry.GetRegistration(typeof(TProjection));
				projection = registration?.Projection as MultiStreamProjection<TProjection>;
			}

			if (projection is null)
			{
				LogNoProjectionRegistered(projectionName);

				_statuses[projectionName] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

			var state = new TProjection();
			var position = new Queries.GlobalStreamPosition(0, DateTimeOffset.MinValue);
			var opts = _options.Value;
			var totalProcessed = 0L;

			while (!cancellationToken.IsCancellationRequested)
			{
				var events = await globalQuery.ReadAllAsync(position, opts.BatchSize, cancellationToken)
					.ConfigureAwait(false);

				if (events.Count == 0)
				{
					break;
				}

				foreach (var storedEvent in events)
				{
					cancellationToken.ThrowIfCancellationRequested();

					try
					{
						var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
						var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType)
							?? throw new InvalidOperationException(
								$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null " +
								$"during rebuild of '{projectionName}'; refusing to skip it.");

						domainEvent = TryUpcastEvent(domainEvent);

						projection.Apply(state, domainEvent);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						// Poison event (deserialize failure, null deserialization, or apply failure): HALT the
						// rebuild at the failed event rather than skip-and-continue (which would advance past it
						// and persist a read model silently missing the event). Rethrow so the rebuild is marked
						// Failed and the partial state is NOT persisted as Completed (ADR-336 Amendment 3a / FR-8).
						LogEventProcessingError(projectionName, storedEvent.EventId, ex);
						throw;
					}

					totalProcessed++;
				}

				// Advance by the GLOBAL stream ordinal (GlobalPosition), not the per-aggregate Version,
				// which skipped/duplicated events across aggregates during rebuild.
				position = new Queries.GlobalStreamPosition(
					events[events.Count - 1].GlobalPosition + 1,
					events[events.Count - 1].Timestamp);

				LogBatchRebuilt(projectionName, events.Count, totalProcessed);

				if (opts.BatchDelay > TimeSpan.Zero)
				{
					await Task.Delay(opts.BatchDelay, cancellationToken).ConfigureAwait(false);
				}
			}

			// Persist the rebuilt state via the projection store (P0 fix: previously discarded)
			var store = _serviceProvider.GetService(typeof(IProjectionStore<TProjection>))
				as IProjectionStore<TProjection>;

			if (store is not null)
			{
				await store.UpsertAsync(projectionName, state, cancellationToken)
					.ConfigureAwait(false);
				LogRebuildPersisted(projectionName);
			}
			else
			{
				LogNoProjectionStoreRegistered(projectionName);
			}

			_statuses[projectionName] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Completed,
				Progress: 100,
				LastRebuiltAt: DateTimeOffset.UtcNow);

			LogRebuildCompleted(projectionName, totalProcessed);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_statuses[projectionName] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Failed,
				Progress: 0,
				LastRebuiltAt: null);

			LogRebuildFailed(projectionName, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task<ProjectionRebuildStatus> GetStatusAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class
	{
		var projectionName = typeof(TProjection).Name;

		var status = _statuses.TryGetValue(projectionName, out var found)
			? found
			: new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Idle,
				Progress: 0,
				LastRebuiltAt: null);

		return Task.FromResult(status);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ProjectionRebuildStatus>> GetAllStatusesAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<ProjectionRebuildStatus> result = [.. _statuses.Values];
		return Task.FromResult(result);
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.ProjectionRebuilt, LogLevel.Information,
		"Projection rebuild started for {ProjectionName}")]
	private partial void LogRebuildStarted(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionCaughtUp, LogLevel.Information,
		"Projection rebuild completed for {ProjectionName}: {TotalEvents} events processed")]
	private partial void LogRebuildCompleted(string projectionName, long totalEvents);

	[LoggerMessage(EventSourcingEventId.ProjectionError, LogLevel.Error,
		"Projection rebuild failed for {ProjectionName}")]
	private partial void LogRebuildFailed(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.ProjectionBatchProcessed, LogLevel.Debug,
		"Projection {ProjectionName}: batch of {BatchSize} events rebuilt, {TotalProcessed} total")]
	private partial void LogBatchRebuilt(string projectionName, int batchSize, long totalProcessed);

	[LoggerMessage(EventSourcingEventId.ProjectionBehind, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Cannot rebuild projection {ProjectionName}")]
	private partial void LogNoGlobalQueryRegistered(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionStopped, LogLevel.Warning,
		"No MultiStreamProjection<{ProjectionName}> registered. Cannot rebuild")]
	private partial void LogNoProjectionRegistered(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionRebuildEventError, LogLevel.Error,
		"Error processing event {EventId} during rebuild of projection {ProjectionName}")]
	private partial void LogEventProcessingError(string projectionName, string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.ProjectionRebuildPersisted, LogLevel.Information,
		"Projection rebuild persisted for {ProjectionName}")]
	private partial void LogRebuildPersisted(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionRebuildNoStore, LogLevel.Warning,
		"No IProjectionStore<{ProjectionName}> registered. Rebuilt state was not persisted. " +
		"Register a projection store to persist rebuild results.")]
	private partial void LogNoProjectionStoreRegistered(string projectionName);

	#endregion
}
