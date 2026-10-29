// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
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
internal sealed partial class ProjectionRebuildService : IProjectionRebuildService, IDisposable
{
	private readonly IServiceProvider _serviceProvider;
	private readonly SemaphoreSlim _rebuildCapacity;
	private readonly IEventSerializer _eventSerializer;
	private readonly IOptions<ProjectionRebuildOptions> _options;
	private readonly ILogger<ProjectionRebuildService> _logger;
	private readonly IUpcastingPipeline? _upcastingPipeline;
	private readonly bool _enableAutoUpcast;
	private readonly ConcurrentDictionary<Type, ProjectionRebuildStatus> _statuses = new();

	// Per-projection in-progress claim set: a rebuild atomically claims its projection name here so a
	// second concurrent StartRebuild for the same projection is rejected rather than interleaving replays and
	// racing the final UpsertAsync. Released in a finally so a later rebuild (after this one finishes) proceeds.
	private readonly ConcurrentDictionary<Type, byte> _rebuildsInProgress = new();

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
		var maximum = options.Value.MaxParallelism;
		ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
		_rebuildCapacity = new SemaphoreSlim(maximum, maximum);

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

	/// <summary>
	/// Removes this projection's subscription member and its checkpoint, so a rebuild folds from zero.
	/// </summary>
	/// <typeparam name="TProjection">The projection being rebuilt.</typeparam>
	/// <param name="services">The rebuild execution scope.</param>
	/// <param name="store">The actual rebuild store.</param>
	/// <param name="mode">The registered projection mode.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when the member has been released, or when there was none.</returns>
	private async Task ReleaseSubscriptionMemberAsync<TProjection>(IServiceProvider services,
        IProjectionStore<TProjection> store, ProjectionMode mode, CancellationToken cancellationToken) where TProjection : class
    {
        var source = mode == ProjectionMode.Inline ? services.GetService<InlineProjectionSource>() : null;
        var checkpoints = source?.Checkpoints ?? services.GetService<Subscriptions.ISubscriptionCheckpointStore>()
            ?? throw new InvalidOperationException("Projection rebuild requires a subscription checkpoint store.");
        checkpoints = ProjectionCapacitySource.Checkpoints(services, checkpoints);
        var options = services.GetService<IOptions<GlobalStreamProjectionOptions>>()?.Value
            ?? throw new InvalidOperationException("Projection rebuild requires configured subscription options.");
        var name = (mode == ProjectionMode.Inline ? "Inline:" : string.Empty) + options.ProjectionName;
        var key = Subscriptions.SubscriptionKeys.ProjectionKey(typeof(TProjection));
        var roster = await checkpoints.ReadSubscriptionAsync(name, cancellationToken).ConfigureAwait(false);
        var member = roster.Members.FirstOrDefault(m => m.ProjectionKey == key);
        if (member is null) { return; }
        var identity = (store.GetService(typeof(IProjectionDestinationIdentity)) as IProjectionDestinationIdentity)?.Identity;
        var cluster = services.GetService<ProjectionCapacity>() is { } capacity
            ? await capacity.ReadScopeAsync(store, cancellationToken).ConfigureAwait(false)
            : store.GetService(typeof(IAsyncProjectionDestinationScope)) is IAsyncProjectionDestinationScope asyncScope
            ? await asyncScope.GetScopeAsync(cancellationToken).ConfigureAwait(false)
            : (store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope;
        var actualScope = ProjectionTenantValidation.Scope(store, cluster);
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(actualScope)
            || member.Destination != identity || member.DestinationScope != actualScope)
        {
            throw new InvalidOperationException("The rebuild destination and tenant boundary do not match the bound projection. Its lifetime has not been retired.");
        }
        if (!await checkpoints.ReleaseSubscriptionMemberAsync(name, key, member.BindingId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Projection '{key}' changed lifetime while preparing its rebuild.");
        }
        LogRebuildReleasedMember(name, key);
    }

    /// <inheritdoc />
    public async Task RebuildAsync<TProjection>(CancellationToken cancellationToken) where TProjection : class, new()
    {
        ProjectionCapacity.ValidateAdmission(ProjectionCapacityKind.Rebuild);
        await using var scope = _serviceProvider.CreateAsyncScope();
        await RebuildCoreAsync<TProjection>(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask<IReadOnlyList<StoredEvent>> ReadEventsAsync(IServiceProvider services, Queries.IGlobalStreamQuery query,
        Queries.GlobalStreamPosition position, int count, CancellationToken token) =>
        ProjectionCapacitySource.ReadAsync(services, query, ct => query.ReadAllAsync(position, count, ct), token);

    private static ValueTask<long> ReadHeadAsync(IServiceProvider services, Queries.IGlobalStreamQuery query, CancellationToken token) =>
        ProjectionCapacitySource.ReadAsync(services, query, query.GetHeadPositionAsync, token);

    private static Queries.IGlobalStreamQuery? ResolveQuery(IServiceProvider services, ProjectionMode mode) =>
        (mode == ProjectionMode.Inline ? services.GetService<InlineProjectionSource>()?.Query : null)
        ?? services.GetService<Queries.IGlobalStreamQuery>();

	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Event deserialization during rebuild uses IEventSerializer which consumers configure with preserved types.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization during rebuild uses IEventSerializer which consumers configure.")]
	private async Task RebuildCoreAsync<TProjection>(IServiceProvider services, CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		var projectionName = typeof(TProjection).Name;

		// Reject an overlapping rebuild of the same projection: the atomic TryAdd claim ensures only
		// one rebuild per projection runs at a time, so two starts cannot interleave reindex/upsert and leave
		// the read model partially overwritten. A second concurrent start fails fast instead of racing.
		if (!_rebuildsInProgress.TryAdd(typeof(TProjection), 0))
		{
			LogRebuildAlreadyInProgress(projectionName);
			throw new InvalidOperationException(
				$"A rebuild for projection '{projectionName}' is already in progress; concurrent rebuilds of the same projection are not allowed.");
		}

		LogRebuildStarted(projectionName);

		_statuses[typeof(TProjection)] = new ProjectionRebuildStatus(
			projectionName,
			ProjectionRebuildState.Rebuilding,
			Progress: 0,
			LastRebuiltAt: null);

		var capacityAcquired = false;
		try
        {
            await _rebuildCapacity.WaitAsync(cancellationToken).ConfigureAwait(false);
            capacityAcquired = true;
            cancellationToken.ThrowIfCancellationRequested();
            var registered = services.GetService<IProjectionRegistry>()?.GetRegistration(typeof(TProjection));
            var mode = registered?.Mode ?? ProjectionMode.Async;
            // Resolve the global stream query from the same source that owns this mode's checkpoint.
			if (ResolveQuery(services, mode) is not { } globalQuery)
			{
				LogNoGlobalQueryRegistered(projectionName);

				_statuses[typeof(TProjection)] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

			// Resolve the multi-stream projection -- check DI first, then IProjectionRegistry (for registered projections)
			var projection = services.GetService(typeof(MultiStreamProjection<TProjection>))
				as MultiStreamProjection<TProjection>;

			if (projection is null
				&& services.GetService(typeof(IProjectionRegistry)) is IProjectionRegistry registry)
			{
				var registration = registry.GetRegistration(typeof(TProjection));
				projection = registration?.Projection as MultiStreamProjection<TProjection>;
			}

			if (projection is null)
			{
				LogNoProjectionRegistered(projectionName);

				_statuses[typeof(TProjection)] = new ProjectionRebuildStatus(
					projectionName,
					ProjectionRebuildState.Failed,
					Progress: 0,
					LastRebuiltAt: null);

				return;
			}

            var store = services.GetService(typeof(IProjectionStore<TProjection>)) as IProjectionStore<TProjection>
                ?? throw new InvalidOperationException($"No projection store is registered for '{projectionName}'.");
            if (!_options.Value.OfflineRebuildAcknowledged)
            {
                throw new InvalidOperationException("In-place rebuild requires OfflineRebuildAcknowledged after all writers and other rebuilds have stopped and drained. For online evolution, populate a separate projection generation and switch its reader pointer.");
            }
            if (services.GetService<ProjectionCapacity>() is { } capacity)
            {
                var resource = await capacity.ResolveResourceAsync(store, cancellationToken).ConfigureAwait(false);
                _ = await capacity.ExecuteAsync(resource, ProjectionCapacityKind.Rebuild, async token =>
                {
                    await ReplayProjectionAsync(services, projection, store, mode, globalQuery, token).ConfigureAwait(false);
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ReplayProjectionAsync(services, projection, store, mode, globalQuery, cancellationToken).ConfigureAwait(false);
            }
		}
		catch (Exception ex)
		{
			_statuses[typeof(TProjection)] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Failed,
				Progress: 0,
				LastRebuiltAt: null);

			LogRebuildFailed(projectionName, ex);
			throw;
		}
		finally
		{
			if (capacityAcquired) { _rebuildCapacity.Release(); }
			// Release the claim so a subsequent rebuild of this projection can proceed once this one ends
			// (success, failure, or cancellation).
			_ = _rebuildsInProgress.TryRemove(typeof(TProjection), out _);
		}
	}

    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Rebuild uses the consumer-configured event serializer and projection metadata.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Rebuild uses the consumer-configured event serializer and projection metadata.")]
    private async Task ReplayProjectionAsync<TProjection>(IServiceProvider services, MultiStreamProjection<TProjection> projection,
        IProjectionStore<TProjection> store, ProjectionMode mode, Queries.IGlobalStreamQuery globalQuery, CancellationToken cancellationToken)
        where TProjection : class, new()
    {
        var projectionName = typeof(TProjection).Name;

            var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);

			// PER PROJECTION ID, not one state for the whole run.
			//
			// This service used to fold every event of the global stream into a single TProjection and
			// write it under the literal type name. Every apply path keys by the registered key selector
			// falling back to the aggregate id, so the two key spaces were disjoint: a rebuild wrote one
			// document under a key no read path queries and left every row a reader actually loads
			// exactly as it was -- while reporting Completed. A singleton projection is not a separate
			// shape to detect; it is a key selector that returns a constant, and it falls out of the
			// same derivation.
			//
			// The fold itself lives in ProjectionReplayFold, which documents the three places a replay
			// must NOT reuse the live apply path: the seed, the already-folded filter, and the settle
			// predicate.
			var fold = new ProjectionReplayFold<TProjection>(projection, store, positioned, services);
            await ReleaseSubscriptionMemberAsync(services, store, mode, cancellationToken).ConfigureAwait(false);

			var position = new Queries.GlobalStreamPosition(0, DateTimeOffset.MinValue);
			var opts = _options.Value;
			var totalProcessed = 0L;

			while (!cancellationToken.IsCancellationRequested)
			{
				var events = await ReadEventsAsync(services, globalQuery, position, opts.BatchSize, cancellationToken)
					.ConfigureAwait(false);

				if (events.Count == 0)
				{
					// AN EMPTY READ IS AMBIGUOUS, and guessing wrong here persists a lie.
					//
					// The global-stream read delivers only the CONTIGUOUS run from our position and stops at
					// the first gap, so an empty result means one of two different things: we are genuinely
					// caught up, or the very next position is absent and everything above it was withheld.
					// Treating the second as the first ends the replay early and writes the projection under
					// a position that asserts a complete fold -- a truncated read model reported as Completed,
					// which is exactly the silent class this service exists to repair.
					//
					// The head position separates them: if the stream has advanced beyond us and we were
					// still handed nothing, the run was withheld rather than exhausted.
					var head = await ReadHeadAsync(services, globalQuery, cancellationToken).ConfigureAwait(false);
					if (head > position.Position)
					{
						// HALT, the same way a poison event halts below, and for the same reason: the partial
						// state must NOT be persisted as Completed. A gap that is merely an in-flight append
						// clears on its own, so re-running the rebuild is the remedy; a PERMANENT hole needs
						// the archival backfill applied first, and the caller is told which position to look at.
						throw new InvalidOperationException(
							$"Rebuild of projection '{projectionName}' stopped at position {position.Position} "
							+ $"while the stream head is {head}: the global-stream read returned nothing because "
							+ $"position {position.Position + 1} is absent, not because the stream was exhausted. "
							+ "Refusing to persist a partial rebuild as Completed. If that position belongs to an "
							+ "append still in flight, re-run the rebuild. If it is permanently absent -- a store "
							+ "archived by a version that DELETED event rows -- apply the archival gap backfill "
							+ "for your provider first.");
					}

					break;
				}

				foreach (var storedEvent in events)
				{
					cancellationToken.ThrowIfCancellationRequested();

					// GDPR erasure tombstones an aggregate's events in place, overwriting EventType with the
					// closed, framework-owned ErasedEventMarker.EventType discriminator ("$erased" -- never a
					// user event type, so ResolveType/DeserializeEvent below cannot resolve it). Recognize the
					// tombstone STRUCTURALLY, before any deserialization attempt, and skip it -- mirroring
					// EventSourcedRepository's rehydration path. This is never a "deserialize failed => assume
					// erased" heuristic (which would mask genuine corruption as erasure); every other
					// deserialization failure still halts the rebuild via the poison-event path below.
					// Without this, a rebuild of any projection whose stream contains an erased subject's
					// aggregate halts permanently at the tombstone -- erasure would make that projection
					// unrebuildable rather than merely omit the subject.
					if (ErasedEventMarker.IsErased(storedEvent.EventType))
					{
						totalProcessed++;
						continue;
					}

					try
					{
						var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
						var domainEvent = _eventSerializer.DeserializeEvent(StoredEventPayload.Require(storedEvent), eventType)
							?? throw new InvalidOperationException(
								$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null " +
								$"during rebuild of '{projectionName}'; refusing to skip it.");

						domainEvent = TryUpcastEvent(domainEvent);

						// Folded through the shared seam, which derives the projection id the SAME way the
						// live apply path does, dispatches through ApplyAsync so an asynchronous handler
						// is not silently dropped, and reproduces the OverrideProjectionId hatch.
						_ = await fold.FoldAsync(
								domainEvent,
								storedEvent.AggregateId,
								storedEvent.AggregateType,
								storedEvent.Version,
								storedEvent.Timestamp,
								storedEvent.GlobalPosition,
								cancellationToken, storedEvent.TenantId)
							.ConfigureAwait(false);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						// Poison event (deserialize failure, null deserialization, or apply failure): HALT the
						// rebuild at the failed event rather than skip-and-continue (which would advance past it
						// and persist a read model silently missing the event). Rethrow so the rebuild is marked
						// Failed and the partial state is NOT persisted as Completed.
						LogEventProcessingError(projectionName, storedEvent.EventId, ex);
						throw;
					}

					totalProcessed++;
				}

				// Advance by the GLOBAL stream ordinal (GlobalPosition), not the per-aggregate Version,
				// which skipped/duplicated events across aggregates during rebuild.
				position = new Queries.GlobalStreamPosition(
					events[events.Count - 1].GlobalPosition,
					events[events.Count - 1].Timestamp);

				LogBatchRebuilt(projectionName, events.Count, totalProcessed);

				if (opts.BatchDelay > TimeSpan.Zero)
				{
					await Task.Delay(opts.BatchDelay, cancellationToken).ConfigureAwait(false);
				}
			}

			if (store is null)
			{
				LogNoProjectionStoreRegistered(projectionName);
			}
			else
			{
				var registration =
					(services.GetService(typeof(IProjectionRegistry)) as IProjectionRegistry)
						?.GetRegistration(typeof(TProjection));

				cancellationToken.ThrowIfCancellationRequested();
				await fold.FlushAsync(
						registration?.SearchTextComputer,
						registration?.SearchTextSetter,
						projectionName,
						cancellationToken)
					.ConfigureAwait(false);

				LogRebuildPersisted(projectionName);
			}

			cancellationToken.ThrowIfCancellationRequested();
			_statuses[typeof(TProjection)] = new ProjectionRebuildStatus(
				projectionName,
				ProjectionRebuildState.Completed,
				Progress: 100,
				LastRebuiltAt: DateTimeOffset.UtcNow);

			LogRebuildCompleted(projectionName, totalProcessed);
    }

	/// <inheritdoc />
	public Task<ProjectionRebuildStatus> GetStatusAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class
	{
		var projectionName = typeof(TProjection).Name;

		var status = _statuses.TryGetValue(typeof(TProjection), out var found)
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

	/// <inheritdoc />
	public void Dispose() => _rebuildCapacity.Dispose();

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

	/// <remarks>
	/// Information, not Debug: a rebuild resetting a durable cursor is an operational event an operator
	/// wants in the record, because it is the point from which the projection re-folds its whole history.
	/// </remarks>
	[LoggerMessage(EventSourcingEventId.ProjectionRebuildReleasedMember, LogLevel.Information,
		"Rebuild released subscription member '{ProjectionKey}' from '{SubscriptionName}'; "
		+ "that projection will re-bind and fold from the beginning.")]
	private partial void LogRebuildReleasedMember(string subscriptionName, string projectionKey);

	[LoggerMessage(EventSourcingEventId.ProjectionRebuildAlreadyInProgress, LogLevel.Warning,
		"Rebuild rejected for {ProjectionName}: a rebuild is already in progress")]
	private partial void LogRebuildAlreadyInProgress(string projectionName);

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
