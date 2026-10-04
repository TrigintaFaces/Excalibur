// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.Dispatch.LeaderElection;
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

	// Optional single-active-processor coordination, resolved the same way the CDC processor resolves it.
	// Null in a single-instance deployment, where the host runs unconditionally. When present, only the
	// leader applies events -- two live readers of one subscription would otherwise each apply a whole
	// batch before either contested the checkpoint, and the checkpoint guards the mark, not the work.
	private readonly ILeaderElection? _leaderElection;

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
		_leaderElection = serviceProvider.GetService(typeof(ILeaderElection)) as ILeaderElection;
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

		// The last position seen DURABLY, tracked separately from _currentPosition because the
		// compare-and-set has to distinguish "no checkpoint yet" (null) from "a checkpoint of 0".
		var durableCheckpoint = lastCheckpoint;

		// The stream head AT START. Every event at or below it was committed before this host began
		// tailing, so delivering it is a catch-up over history, not live delivery. Handlers that suppress
		// side effects on replay rely on being told which one they are seeing, and hard-coding the answer
		// defeats exactly the guard that exists for this case.
		long headAtStart;
		try
		{
			headAtStart = await globalStreamQuery.GetHeadPositionAsync(stoppingToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// The head is an optimisation for honesty, never a precondition for processing. If it cannot
			// be read, report every event as a replay: that is the conservative answer, because a handler
			// guarding on replay will then suppress rather than double-apply.
			LogAsyncProjectionHeadUnavailable(ex);
			headAtStart = long.MaxValue;
		}

		LogAsyncProjectionHostStarted(asyncRegistrations.Count);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// STANDBY, not exit. A host that is not the leader must keep running and keep asking:
				// leadership moves on a deploy, and a process that stopped asking would never take over.
				if (_leaderElection is not null && _leaderElection.CurrentLeadership is null)
				{
					LogAsyncProjectionStandby();
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
					continue;
				}

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
				// mirrors GlobalStreamProjectionHost.
				var deserialized = new List<DeserializedEvent>(events.Count);
				var poisonEncountered = false;

				// The last event this batch made progress past, and how many events it accounted for. Tracked
				// separately from the deserialized list because an erased (tombstoned) event is progress
				// without being a deliverable event: a batch made entirely of tombstones deserializes to
				// nothing and must still advance, or the checkpoint sticks and the batch is re-read forever.
				StoredEvent? lastProcessed = null;
				var processedCount = 0;

				foreach (var storedEvent in events)
				{
					stoppingToken.ThrowIfCancellationRequested();

					// An erased (GDPR-tombstoned) event carries the reserved marker in place of its type and a
					// nulled payload, so no serializer can resolve it. Recognize it STRUCTURALLY, before any
					// deserialization attempt, and advance past it: it is a permanent, legitimate part of the
					// stream, not a poison event. Halting here would let an erasure request stop the projection
					// host permanently. It is never handed to a projection handler, so it cannot populate state.
					// Only the reserved marker is skipped: any other unresolvable event is still poison below.
					if (ErasedEventMarker.IsErased(storedEvent.EventType))
					{
						LogErasedEventSkipped(storedEvent.EventId, storedEvent.GlobalPosition);
						lastProcessed = storedEvent;
						processedCount++;
						continue;
					}

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
					lastProcessed = storedEvent;
					processedCount++;
				}

				// Dispatch the good prefix in GLOBAL ORDER, splitting only at replay/live boundaries.
				//
				// It used to be grouped by aggregate, which was wrong for any projection not keyed by the
				// aggregate. A keyed projection maps events from many aggregates onto one projection id,
				// so grouping wrote that id once per group -- and group order is first-seen order, not
				// stream order. One batch could therefore write a projection at position 7 and then at
				// position 6, with one reader, no concurrency and no crash. A store that refuses a
				// non-advancing write would then reject the second and drop the event entirely, which is
				// the worse failure: grouping had to go before the position could be enforced.
				//
				// Delivering each run whole is correct because the apply path derives the
				// projection id and the handler context PER EVENT rather than per call.
				var applyFaultEncountered = false;
				for (var runStart = 0; runStart < deserialized.Count && !applyFaultEncountered;)
				{
					stoppingToken.ThrowIfCancellationRequested();
					var isReplay = deserialized[runStart].Stored.GlobalPosition <= headAtStart;
					var runEnd = runStart + 1;
					while (runEnd < deserialized.Count
						&& (deserialized[runEnd].Stored.GlobalPosition <= headAtStart) == isReplay)
					{
						runEnd++;
					}

					// A batch spanning the startup head needs two contexts. Using its final event's
					// replay flag for the whole batch would label pre-start events as live.
					var batch = new List<ProjectionEvent>(runEnd - runStart);
					for (var index = runStart; index < runEnd; index++)
					{
						var item = deserialized[index];
						batch.Add(new ProjectionEvent(
							item.Domain, item.Stored.AggregateId, item.Stored.GlobalPosition));
					}

					var last = deserialized[runEnd - 1].Stored;

					// The batch-level context describes the LAST event in the batch. Its aggregate-scoped
					// members are no longer used for identity -- every apply reads those off the event --
					// so they are carried for handlers that still want the batch's high-water mark.
					var context = new EventNotificationContext(
						last.AggregateId,
						last.AggregateType,
						last.Version,
						last.Timestamp,
						IsReplay: isReplay,
						GlobalPosition: last.GlobalPosition);

					applyFaultEncountered = await DispatchToProjectionsAsync(
						asyncRegistrations, batch, context, stoppingToken).ConfigureAwait(false);
					runStart = runEnd;
				}

				// HALT-at-failure: when any projection's apply faulted, DO NOT advance the checkpoint past
				// this batch. Leaving the position unadvanced means the batch is reprocessed on the next
				// read (at-least-once; applies are idempotent) rather than silently skipped — the read
				// model can never drift from the event log. Mirrors the deserialize-poison halt above and
				// GlobalStreamProjectionHost. A clean batch advances normally.
				//
				// The advance is gated on lastProcessed rather than on the deserialized list, so a batch whose
				// events were ALL erased tombstones still advances. A batch that made no progress at all (its
				// first event was poison) leaves lastProcessed null and the position untouched.
				if (lastProcessed is not null && !applyFaultEncountered)
				{
					// Advance ONLY to the last processed event's GLOBAL ordinal (GlobalPosition), never the
					// per-aggregate Version. The poison event (and everything after it) stays unread/unskipped.
					_currentPosition = new GlobalStreamPosition(lastProcessed.GlobalPosition, lastProcessed.Timestamp);
					_eventsSinceCheckpoint += processedCount;

					// Checkpoint when threshold reached (only ever the last-good position; never past a poison event).
					if (_eventsSinceCheckpoint >= opts.CheckpointInterval)
					{
						// Compare-and-set, not a blind write: if another reader of this subscription has
						// moved the checkpoint since we last saw it, writing ours would drag the mark
						// BACKWARDS and redeliver everything between the two positions.
						var outcome = await _checkpointStore.AdvanceCheckpointAsync(
							checkpointName, durableCheckpoint, _currentPosition.Position, stoppingToken)
							.ConfigureAwait(false);

						if (outcome == CheckpointAdvanceOutcome.Superseded)
						{
							// RESUME FROM THE WINNER'S MARK. Do not exit.
							//
							// Exiting is only safe if the winner is guaranteed to keep running, and nothing
							// guarantees that: after a rolling deploy the surviving process would be the one
							// that stood down, no reader would be processing, and the application would still
							// report healthy. A permanent exit converts a transient overlap into a permanent
							// stall, which is the worse failure because nothing downstream can detect it.
							//
							// Adopting the winner's mark also stops this reader re-reading the span the
							// winner already covered.
							LogAsyncProjectionCheckpointSuperseded(_currentPosition.Position);

							var winnersMark = await _checkpointStore
								.GetCheckpointAsync(checkpointName, stoppingToken).ConfigureAwait(false);

							durableCheckpoint = winnersMark;
							if (winnersMark is { } mark)
							{
								_currentPosition = new GlobalStreamPosition(mark, DateTimeOffset.MinValue);
							}

							_eventsSinceCheckpoint = 0;

							// Mark the host unhealthy. A contested subscription means two readers applied
							// the same span, and that is a condition an operator must see rather than a
							// line in a log nobody is tailing at the time.
							_healthState?.RecordInlineError(nameof(AsyncProjectionProcessingHost));
							LogAsyncProjectionContested();

							await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
							continue;
						}

						durableCheckpoint = _currentPosition.Position;
						LogAsyncProjectionCheckpointSaved(_currentPosition.Position);
						_eventsSinceCheckpoint = 0;
					}

					LogAsyncProjectionBatchProcessed(processedCount, _currentPosition.Position);
				}

				// On a poison event OR an apply fault, back off before re-reading so we don't tight-loop on a
				// permanent failure; the next read resumes from the unadvanced checkpoint (reprocess, not skip).
				if (poisonEncountered || applyFaultEncountered)
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
				var shutdownOutcome = await _checkpointStore.AdvanceCheckpointAsync(
					checkpointName, durableCheckpoint, _currentPosition.Position, CancellationToken.None)
					.ConfigureAwait(false);

				if (shutdownOutcome == CheckpointAdvanceOutcome.Superseded)
				{
					// Nothing to stop -- we are already shutting down. Recorded because a superseded
					// shutdown write means another reader took the subscription over while we ran.
					LogAsyncProjectionCheckpointSuperseded(_currentPosition.Position);
				}
				else
				{
					durableCheckpoint = _currentPosition.Position;
					LogAsyncProjectionCheckpointSaved(_currentPosition.Position);
				}
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
		return _eventSerializer.DeserializeEvent(StoredEventPayload.Require(storedEvent), eventType)
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
	/// Dispatches domain events to all async projection registrations concurrently.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if at least one projection's apply faulted (each projection is still
	/// attempted — fault-independence); otherwise <see langword="false"/>. The caller MUST NOT advance the
	/// checkpoint past a batch that reports a fault, so a failed apply is reprocessed rather than silently
	/// skipped (read-model integrity; parity with the deserialize-poison halt and GlobalStreamProjectionHost).
	/// </returns>
	private async Task<bool> DispatchToProjectionsAsync(
		IReadOnlyList<ProjectionRegistration> registrations,
		List<ProjectionEvent> domainEvents,
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

		var anyFaulted = false;
		for (var j = 0; j < tasks.Length; j++)
		{
			try
			{
				await tasks[j].ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Catch general exceptions -- partial failure; log, record, and report to halt the batch
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// Fault-independence: still await every projection so an independent one is not abandoned.
				// But REPORT the fault so the caller does not advance the checkpoint past it (halt-at-failure),
				// preventing silent read-model drift from the event log.
				anyFaulted = true;
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

		return anyFaulted;
	}

	private async Task ApplyInScopeAsync(
		ProjectionRegistration registration,
		List<ProjectionEvent> domainEvents,
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

	[LoggerMessage(EventSourcingEventId.ErasedEventSkipped, LogLevel.Debug,
		"Skipping erased (tombstoned) event {EventId} at global position {GlobalPosition}; advancing past it")]
	private partial void LogErasedEventSkipped(string eventId, long globalPosition);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionCheckpointSaved, LogLevel.Debug,
		"Async projection checkpoint saved at position {Position}.")]
	private partial void LogAsyncProjectionCheckpointSaved(long position);

	[LoggerMessage(EventSourcingEventId.ProjectionCheckpointSuperseded, LogLevel.Warning,
		"Async projection host could not advance its checkpoint to {Position}: another reader has moved "
		+ "it. This host is standing down; the other reader owns the subscription.")]
	private partial void LogAsyncProjectionCheckpointSuperseded(long position);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionEventError, LogLevel.Error,
		"Poison event {EventId} halted the async projection host; checkpoint not advanced past it.")]
	private partial void LogAsyncProjectionEventError(string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionDispatchError, LogLevel.Error,
		"Error dispatching events to async projection {ProjectionName}.")]
	private partial void LogAsyncProjectionDispatchError(string projectionName, Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHostError, LogLevel.Error,
		"Async projection processing host encountered an error.")]
	private partial void LogAsyncProjectionHostError(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionHeadUnavailable, LogLevel.Warning,
		"Could not read the global stream head at startup, so every event this host delivers is reported "
		+ "as a replay. Handlers that suppress side effects on replay will suppress them.")]
	private partial void LogAsyncProjectionHeadUnavailable(Exception ex);

	[LoggerMessage(EventSourcingEventId.AsyncProjectionContested, LogLevel.Error,
		"Another reader advanced this subscription's checkpoint, so two readers are processing one "
		+ "subscription and events in the contested span may have been applied twice. Register leader "
		+ "election, or run a single instance of this host.")]
	private partial void LogAsyncProjectionContested();

	[LoggerMessage(EventSourcingEventId.AsyncProjectionStandby, LogLevel.Debug,
		"Async projection processing is on standby: another instance holds leadership for this subscription.")]
	private partial void LogAsyncProjectionStandby();

	[LoggerMessage(EventSourcingEventId.AsyncProjectionNoGlobalStreamQuery, LogLevel.Warning,
		"No IGlobalStreamQuery registered. Async projection processing cannot start. " +
		"Ensure your event store provider (e.g., UseSqlServer) registers IGlobalStreamQuery.")]
	private partial void LogNoGlobalStreamQuery();

	#endregion Logging
}
