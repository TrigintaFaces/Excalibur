// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Background service that hosts a <see cref="IGlobalStreamProjection{TState}"/>,
/// continuously reading from the global event stream via <see cref="IGlobalStreamQuery"/>
/// and applying events to the projection state.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// This host manages the lifecycle of a global stream projection, including:
/// <list type="bullet">
/// <item>Reading events from the global stream in configurable batches</item>
/// <item>Applying events to the projection via <see cref="IGlobalStreamProjection{TState}.ApplyAsync"/></item>
/// <item>Tracking processing position for checkpoint/resume</item>
/// <item>Graceful shutdown with cancellation support</item>
/// </list>
/// </para>
/// <para>
/// Events from the event store are <see cref="StoredEvent"/> records and must be
/// deserialized to <see cref="IDomainEvent"/> before applying to the projection.
/// This host uses <see cref="IEventSerializer"/> for deserialization.
/// </para>
/// </remarks>
public sealed partial class GlobalStreamProjectionHost<TState> : BackgroundService
	where TState : class, new()
{
	// This host is a singleton BackgroundService, but IGlobalStreamQuery / IGlobalStreamProjection /
	// ISubscriptionCheckpointStore / ICursorMapStore are typically scoped (provider-specific SQL impls).
	// Capturing them directly would be a captive dependency (scope-validation throw, or a connection
	// pinned for the process lifetime), so they are resolved from a fresh scope per polling cycle
	// — the same pattern the sibling AsyncProjectionProcessingHost documents and uses.
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IEventSerializer _eventSerializer;
	private readonly IOptions<GlobalStreamProjectionOptions> _options;
	private readonly ILogger<GlobalStreamProjectionHost<TState>> _logger;
	private readonly ProjectionObservability? _observability;
	private readonly ProjectionHealthState? _healthState;
	private readonly IUpcastingPipeline? _upcastingPipeline;
	private readonly bool _enableAutoUpcast;

	private readonly Dictionary<string, long> _pendingCursorUpdates = new(StringComparer.Ordinal);
	private GlobalStreamPosition _currentPosition = GlobalStreamPosition.Start;

	// The last position durably persisted to the checkpoint store. On a flush/processing failure the host
	// rolls _currentPosition back to this so it resumes from the durable checkpoint and reprocesses the
	// un-checkpointed events — the checkpoint, not the in-memory position, is the source of truth.
	private GlobalStreamPosition _checkpointedPosition = GlobalStreamPosition.Start;

	/// <summary>
	/// The position this host last saw DURABLY stored, or null when it has never stored one.
	/// </summary>
	/// <remarks>
	/// Kept separately from <see cref="_checkpointedPosition"/> because that field cannot express "no
	/// checkpoint yet" -- it defaults to position 0, which is also a legitimate stored value. The
	/// compare-and-set needs those two states distinguished: a host that believes no checkpoint exists
	/// must lose to a writer that has since created one.
	/// </remarks>
	private long? _durableCheckpoint;
	private long _eventsSinceCheckpoint;

	/// <summary>
	/// Initializes a new instance of the <see cref="GlobalStreamProjectionHost{TState}"/> class.
	/// </summary>
	/// <param name="scopeFactory">
	/// The scope factory used to resolve the (typically scoped) global-stream query, projection, checkpoint
	/// store, and optional cursor-map store from a fresh scope per polling cycle (avoids a captive dependency
	/// in this singleton host).
	/// </param>
	/// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
	/// <param name="options">The projection host options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="serviceProvider">The service provider for resolving internal observability services.</param>
	public GlobalStreamProjectionHost(
		IServiceScopeFactory scopeFactory,
		IEventSerializer eventSerializer,
		IOptions<GlobalStreamProjectionOptions> options,
		ILogger<GlobalStreamProjectionHost<TState>> logger,
		IServiceProvider serviceProvider)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Resolve internal observability types from DI (optional, never fails)
		ArgumentNullException.ThrowIfNull(serviceProvider);
		_observability = serviceProvider.GetService(typeof(ProjectionObservability))
			as ProjectionObservability;
		_healthState = serviceProvider.GetService(typeof(ProjectionHealthState))
			as ProjectionHealthState;

		// Apply the same upcasting the write side uses (EventSourcedRepository) so the read model does not
		// diverge from the write model when an event schema evolves. Optional: resolved from DI, never fails.
		_upcastingPipeline = serviceProvider.GetService(typeof(IUpcastingPipeline)) as IUpcastingPipeline;
		_enableAutoUpcast = (serviceProvider.GetService(typeof(IOptions<UpcastingOptions>)) as IOptions<UpcastingOptions>)
			?.Value.EnableAutoUpcastOnReplay ?? false;
	}

	/// <summary>
	/// Upcasts a deserialized event to its latest registered version when auto-upcast-on-replay is enabled, so the
	/// projection applies the same (current-schema) event the write-side aggregate would. Mirrors the repository.
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
	/// The checkpoint key for this host, derived from <typeparamref name="TState"/> unless the consumer
	/// named it explicitly.
	/// </summary>
	/// <remarks>
	/// A checkpoint is keyed by name, so two hosts sharing a name share one mark. This type is generic and
	/// is registered once per state type, so the state type is a name that is unique by construction —
	/// deriving it makes the collision inexpressible rather than merely discouraged. An explicit name
	/// still wins, which is what a consumer running two hosts over the same state type needs.
	/// </remarks>
	private string SubscriptionName
	{
		get
		{
			var configured = _options.Value.ProjectionName;
			return string.IsNullOrWhiteSpace(configured)
				|| string.Equals(configured, GlobalStreamProjectionOptions.DefaultProjectionName, StringComparison.Ordinal)
					? "GlobalStreamProjection:" + typeof(TState).FullName
					: configured;
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection host requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = _options.Value;
		var state = new TState();

		// Restore checkpoint position from last run (scoped resolution — no captive dependency).
		long? lastCheckpoint;
		await using (var restoreScope = _scopeFactory.CreateAsyncScope())
		{
			var restoreCheckpointStore = restoreScope.ServiceProvider.GetRequiredService<ISubscriptionCheckpointStore>();
			lastCheckpoint = await restoreCheckpointStore.GetCheckpointAsync(SubscriptionName, stoppingToken)
				.ConfigureAwait(false);
		}

		if (lastCheckpoint.HasValue)
		{
			_currentPosition = new GlobalStreamPosition(lastCheckpoint.Value, DateTimeOffset.MinValue);
			_checkpointedPosition = _currentPosition;
		}

		_durableCheckpoint = lastCheckpoint;

		LogProjectionHostStarted(SubscriptionName);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Resolve the (scoped) query/projection/stores from a fresh scope per polling cycle so this
				// singleton host never captures a scoped dependency. Host-level accumulators
				// (_currentPosition, _checkpointedPosition, _eventsSinceCheckpoint, _pendingCursorUpdates,
				// state) stay on the host; only the DI-resolved collaborators are per-cycle.
				await using var scope = _scopeFactory.CreateAsyncScope();
				var provider = scope.ServiceProvider;
				var globalStreamQuery = provider.GetRequiredService<IGlobalStreamQuery>();
				var projection = provider.GetRequiredService<IGlobalStreamProjection<TState>>();
				var checkpointStore = provider.GetRequiredService<ISubscriptionCheckpointStore>();
				var cursorMapStore = provider.GetService<ICursorMapStore>();

				var events = await globalStreamQuery.ReadAllAsync(
					_currentPosition,
					opts.BatchSize,
					stoppingToken).ConfigureAwait(false);

				if (events.Count == 0)
				{
					// No new events; wait before polling again
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
					continue;
				}

				// Apply each event. On a poison event (deserialize failure, null deserialization, or
				// apply failure) STOP at that event and do NOT advance the checkpoint past it, so the
				// read model can never silently drift from the event log. The host marks itself
				// unhealthy and re-reads from the last good position on the next poll: a transient
				// failure self-heals on retry; a permanent one stays unhealthy until an operator acts.
				GlobalStreamPosition? lastGoodPosition = null;
				var poisonEncountered = false;

				foreach (var storedEvent in events)
				{
					stoppingToken.ThrowIfCancellationRequested();

					// An erased (GDPR-tombstoned) event carries the reserved marker in place of its type and a
					// nulled payload, so no serializer can resolve it. Recognize it STRUCTURALLY, before any
					// deserialization attempt, and advance the checkpoint past it: it is a permanent, legitimate
					// part of the stream, not a poison event. Treating it as poison would halt this host at the
					// first tombstone forever, so honouring an erasure request would stop the projection. It is
					// never handed to a projection handler, so it cannot populate state. Only the reserved marker
					// is skipped: any other unresolvable event is still poison and still halts below.
					if (ErasedEventMarker.IsErased(storedEvent.EventType) || storedEvent.EventData is null)
					{
						LogErasedEventSkipped(storedEvent.EventId, storedEvent.GlobalPosition);

						// Advance the per-stream cursor alongside the checkpoint. Erasure preserves the event's
						// stream identity and version, and advancing the checkpoint while leaving the cursor map
						// behind is the one direction this host treats as unsafe (see the ordering note below):
						// a multi-stream resume from a checkpoint that is ahead of the cursor map can skip events.
						if (cursorMapStore is not null)
						{
							var erasedStreamKey = $"{storedEvent.AggregateType}:{storedEvent.AggregateId}";
							_pendingCursorUpdates[erasedStreamKey] = storedEvent.Version;
						}

						lastGoodPosition = new GlobalStreamPosition(
							storedEvent.GlobalPosition,
							storedEvent.Timestamp);
						_eventsSinceCheckpoint++;
						continue;
					}

					try
					{
						var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
						var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType)
							?? throw new InvalidOperationException(
								$"Event '{storedEvent.EventId}' (type '{storedEvent.EventType}') deserialized to null; refusing to skip it.");

						domainEvent = TryUpcastEvent(domainEvent);

						await projection.ApplyAsync(domainEvent, state, stoppingToken)
							.ConfigureAwait(false);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						// Poison event: record + mark unhealthy, then HALT this batch at the failed event.
						// We do NOT advance past it (no silent skip, no checkpoint past an unapplied event).
						LogEventProcessingError(SubscriptionName, storedEvent.EventId, ex);
						try
						{
							_observability?.RecordError(typeof(TState).Name, ex.GetType().Name);
						}
						catch
						{
							/* swallow */
						}

						try
						{
							_healthState?.RecordInlineError(typeof(TState).Name);
						}
						catch
						{
							/* swallow */
						}

						poisonEncountered = true;
						break;
					}

					// Event applied successfully — track per-stream cursor and the last good
					// global checkpoint position (the GLOBAL ordinal, not the per-aggregate Version).
					if (cursorMapStore is not null)
					{
						var streamKey = $"{storedEvent.AggregateType}:{storedEvent.AggregateId}";
						_pendingCursorUpdates[streamKey] = storedEvent.Version;
					}

					lastGoodPosition = new GlobalStreamPosition(
						storedEvent.GlobalPosition,
						storedEvent.Timestamp);
					_eventsSinceCheckpoint++;
				}

				// Advance ONLY to the last successfully-applied event's global position. On a poison
				// event this leaves the checkpoint at/just-before it so it is reprocessed on the next
				// read, never skipped. If the first event in the batch was poison, position is unchanged.
				if (lastGoodPosition is not null)
				{
					_currentPosition = lastGoodPosition;
				}

				// Checkpoint if needed -- persist position so restarts resume here (only ever the
				// last-good position; never past a poison event).
				if (_eventsSinceCheckpoint >= opts.CheckpointInterval)
				{
					// Save the cursor map FIRST, then advance the checkpoint. The checkpoint is the source
					// of truth on restart (the host resumes reading events AFTER the checkpoint position),
					// so the only safe partial-failure direction is cursor-map-ahead-of-checkpoint: if the
					// checkpoint write fails, the cursor map is merely ahead and a restart reprocesses
					// (idempotent whole-map replace), never skips. Writing the checkpoint first would let it
					// advance past an un-saved cursor map, and a multi-stream resume would then skip events.
					// Invariant: cursor-map >= checkpoint, NEVER checkpoint > cursor-map. (The two stores are
					// decoupled and neither is transaction-bearing, so ordering — not a cross-store
					// transaction — is the fix.)
					if (cursorMapStore is not null && _pendingCursorUpdates.Count > 0)
					{
						await cursorMapStore.SaveCursorMapAsync(
								SubscriptionName, _pendingCursorUpdates, stoppingToken)
							.ConfigureAwait(false);
						_pendingCursorUpdates.Clear();
					}

					// Compare-and-set, not a blind write. If another reader of this subscription has moved
					// the checkpoint since we last saw it, writing ours would drag the mark BACKWARDS and
					// the whole span between the two positions would be delivered again.
					var outcome = await checkpointStore.AdvanceCheckpointAsync(
							SubscriptionName, _durableCheckpoint, _currentPosition.Position, stoppingToken)
						.ConfigureAwait(false);

					if (outcome == CheckpointAdvanceOutcome.Superseded)
					{
						// STAND BY, DO NOT EXIT. Returning here ends the background service permanently,
						// and that is only safe if the winner is guaranteed to keep running. Nothing
						// guarantees it: after a rolling deploy the surviving process would be the one
						// that stood down, no reader would be processing this subscription, and the
						// application would still report healthy. A permanent exit turns a transient
						// overlap into a permanent stall, which is the worse failure precisely because
						// nothing downstream can detect it.
						LogCheckpointSuperseded(SubscriptionName, _currentPosition.Position);

						// Adopt the winner's mark so this reader stops re-reading the span already
						// covered, then keep polling. If the winner stops, this reader takes over.
						_durableCheckpoint = await checkpointStore
							.GetCheckpointAsync(SubscriptionName, stoppingToken).ConfigureAwait(false);

						if (_durableCheckpoint is { } winnersMark)
						{
							_currentPosition = new GlobalStreamPosition(winnersMark, DateTimeOffset.MinValue);
							_checkpointedPosition = _currentPosition;
						}

						_eventsSinceCheckpoint = 0;
						continue;
					}

					// The checkpoint is now durable — record it as the rollback target.
					_durableCheckpoint = _currentPosition.Position;
					_checkpointedPosition = _currentPosition;

					LogCheckpointSaved(SubscriptionName, _currentPosition.Position);
					_eventsSinceCheckpoint = 0;
				}

				// Report lag as current position (consumers compute actual lag externally)
				try
				{
					_healthState?.AsyncLag = _currentPosition.Position;
				}
				catch
				{
					/* swallow */
				}

				LogBatchProcessed(SubscriptionName, events.Count, _currentPosition.Position);

				// On a poison event, back off before re-reading so we don't tight-loop on a permanent
				// failure; the next read resumes from the unadvanced checkpoint (reprocess, not skip).
				if (poisonEncountered)
				{
					await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogProjectionHostError(SubscriptionName, ex);

				// Roll back to the last durably-checkpointed position and discard the pending cursor updates
				// for the un-checkpointed batch. A failed flush (cursor-map OR checkpoint write) must never
				// leave the in-memory position or the pending buffer ahead of what is durable: the host
				// re-reads from the checkpoint and reprocesses (idempotent), so no cursor entry is lost, the
				// checkpoint never advances past its cursor map, and _pendingCursorUpdates stays bounded
				// across repeated failures.
				_currentPosition = _checkpointedPosition;
				_eventsSinceCheckpoint = 0;
				_pendingCursorUpdates.Clear();

				// Wait before retrying to avoid tight error loops
				await Task.Delay(opts.IdlePollingInterval, stoppingToken).ConfigureAwait(false);
			}
		}

		// Persist final checkpoint position on graceful shutdown (scoped resolution — no captive dependency).
		if (_eventsSinceCheckpoint > 0)
		{
			try
			{
				await using var finalScope = _scopeFactory.CreateAsyncScope();
				var finalProvider = finalScope.ServiceProvider;
				var checkpointStore = finalProvider.GetRequiredService<ISubscriptionCheckpointStore>();
				var cursorMapStore = finalProvider.GetService<ICursorMapStore>();

				// Same ordering invariant as the periodic checkpoint: cursor map FIRST, checkpoint LAST,
				// so a partial failure can only leave the cursor map ahead of the checkpoint, never the
				// reverse (which would skip events on a multi-stream resume).
				if (cursorMapStore is not null && _pendingCursorUpdates.Count > 0)
				{
					await cursorMapStore.SaveCursorMapAsync(
							SubscriptionName, _pendingCursorUpdates, CancellationToken.None)
						.ConfigureAwait(false);
					_pendingCursorUpdates.Clear();
				}

				var shutdownOutcome = await checkpointStore.AdvanceCheckpointAsync(
						SubscriptionName, _durableCheckpoint, _currentPosition.Position, CancellationToken.None)
					.ConfigureAwait(false);

				if (shutdownOutcome == CheckpointAdvanceOutcome.Superseded)
				{
					// We are shutting down anyway, so there is nothing to stop. Record it: a superseded
					// shutdown write means another reader took this subscription over while we ran, which
					// is worth knowing when reconciling what each instance processed.
					LogCheckpointSuperseded(SubscriptionName, _currentPosition.Position);
				}
				else
				{
					_durableCheckpoint = _currentPosition.Position;
					_checkpointedPosition = _currentPosition;

					LogCheckpointSaved(SubscriptionName, _currentPosition.Position);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogProjectionHostError(SubscriptionName, ex);
			}
		}

		LogProjectionHostStopped(SubscriptionName);
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.ProjectionStarted, LogLevel.Information,
		"Global stream projection host started for {ProjectionName}")]
	private partial void LogProjectionHostStarted(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionStopped, LogLevel.Information,
		"Global stream projection host stopped for {ProjectionName}")]
	private partial void LogProjectionHostStopped(string projectionName);

	[LoggerMessage(EventSourcingEventId.ProjectionCheckpointSaved, LogLevel.Debug,
		"Checkpoint saved for {ProjectionName} at position {Position}")]
	private partial void LogCheckpointSaved(string projectionName, long position);

	[LoggerMessage(EventSourcingEventId.ProjectionCheckpointSuperseded, LogLevel.Warning,
		"Projection {ProjectionName} could not advance its checkpoint to {Position}: another reader has "
		+ "moved it. This host is standing down; the other reader owns the subscription.")]
	private partial void LogCheckpointSuperseded(string projectionName, long position);

	[LoggerMessage(EventSourcingEventId.ProjectionBatchProcessed, LogLevel.Debug,
		"Batch of {EventCount} events processed for {ProjectionName}, position now at {Position}")]
	private partial void LogBatchProcessed(string projectionName, int eventCount, long position);

	[LoggerMessage(EventSourcingEventId.ProjectionError, LogLevel.Error,
		"Error processing event {EventId} in projection {ProjectionName}")]
	private partial void LogEventProcessingError(string projectionName, string eventId, Exception ex);

	[LoggerMessage(EventSourcingEventId.ErasedEventSkipped, LogLevel.Debug,
		"Skipping erased (tombstoned) event {EventId} at global position {GlobalPosition}; advancing past it")]
	private partial void LogErasedEventSkipped(string eventId, long globalPosition);

	[LoggerMessage(EventSourcingEventId.ProjectionBehind, LogLevel.Error,
		"Global stream projection host error for {ProjectionName}")]
	private partial void LogProjectionHostError(string projectionName, Exception ex);

	#endregion Logging
}
