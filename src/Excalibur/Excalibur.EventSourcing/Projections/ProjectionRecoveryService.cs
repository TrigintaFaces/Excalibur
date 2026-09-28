// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// How many times a replay may be refused by a concurrent writer before recovery gives up.
	/// </summary>
	/// <remarks>
	/// A bound rather than an unbounded loop: a projection under continuous write pressure would
	/// otherwise spin forever, and a recovery that never returns is indistinguishable from one that
	/// hung. Exhausting the bound is reported, never swallowed.
	/// </remarks>
	private const int MaxRecoveryAttempts = 5;

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
	/// <remarks>
	/// <para>
	/// <b>Recovery competes with the live processor, so it cannot simply overwrite.</b> It replays the
	/// aggregate's whole history into fresh state and then writes THROUGH the same position-conditional
	/// seam the incremental path uses, claiming the position it read before the replay began. A write
	/// refused because a processor moved the row is replayed again rather than forced: forcing it would
	/// discard whatever the processor folded in the meantime, which is the data-loss this seam exists to
	/// prevent.
	/// </para>
	/// <para>
	/// A store that records no positions keeps the previous unconditional behaviour, because there is
	/// nothing to be conditional on.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Projection persistence is inherently dynamic; the interface this implements cannot carry the requirement, so consumers preserve their projection types.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Projection persistence requires type metadata; the interface this implements cannot carry the requirement, so consumers preserve their projection types.")]
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

		// REFUSED, not silently wrong. Recovery replays ONE aggregate onto a FRESH state and writes it
		// under the aggregate id. That is correct only when the aggregate id IS the projection id.
		//
		// With a KeyedBy selector registered the projection id is derived from the event -- a category, a
		// tenant, a date -- and many aggregates fold into one key. Writing the aggregate id would target a
		// key no reader queries; writing the DERIVED key would be worse, because this replay saw one
		// aggregate's events and would overwrite the key with a state missing every other aggregate that
		// contributes to it. Neither is recoverable after the fact and both succeed silently.
		//
		// A keyed projection is rebuilt whole, not one aggregate at a time. The same limit applies to an
		// asynchronous handler that redirects through OverrideProjectionId; that cannot be detected before
		// the handler runs, so it is stated as a consumer obligation rather than checked here.
		if (projection.HasKeySelectors)
		{
			throw new InvalidOperationException(
				$"Projection '{typeof(TProjection).Name}' registers a KeyedBy selector, so its projection "
				+ "ids are derived from event data and each one is fed by many aggregates. Recovering a "
				+ $"single aggregate ('{aggregateId}') cannot produce a correct row for such a key: this "
				+ "replay sees only that aggregate's events, so any key it wrote would be missing every "
				+ "other aggregate's contribution. Rebuild the whole projection instead.");
		}

		var store = _serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
		var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);

		for (var attempt = 1; ; attempt++)
		{
			// Read the position BEFORE the replay, so the write can claim to advance from the state the
			// replay started against. Reading it afterwards would claim a position the replay never saw.
			long? readAt = null;
			if (positioned is not null)
			{
				(_, var readAtPos) = await positioned.GetWithPositionAsync(aggregateId, cancellationToken)
					.ConfigureAwait(false);
				readAt = readAtPos.ExpectedPositionOrNull;
			}

			var replayed = await ReplayAsync(projection, aggregateId, aggregateType, cancellationToken)
				.ConfigureAwait(false);

			if (positioned is null || (readAt is null && replayed.HighestPosition is null))
			{
				// Either the store does not record positions, or there is neither a position to
				// preserve nor one to claim. Nothing can be conditional on anything.
				//
				// THE STATE HERE IS A COMPLETE FOLD, and saying so is what stops this path being the
				// largest producer of unplaceable rows in the system. A fully erased aggregate yields no
				// position BY CONSTRUCTION -- every event is a tombstone and the replay skips them all --
				// so recovery lands here on exactly the compliance path that runs most often. Writing it
				// through the blind surface would record "not a fold over any prefix", which is false:
				// the replay folded the whole stream. The next batch would then be REFUSED against a row
				// this method had just correctly rebuilt.
				if (positioned is not null)
				{
					await positioned.UpsertUnnumberedAsync(aggregateId, replayed.State, cancellationToken)
						.ConfigureAwait(false);
				}
				else
				{
					// No position is recorded by this store at all, so there is no third state to state.
					await store.UpsertAsync(aggregateId, replayed.State, cancellationToken)
						.ConfigureAwait(false);
				}

				LogRecovered(aggregateId, replayed.EventCount, typeof(TProjection).Name);
				return;
			}

			// ADVANCE vs RE-FOLD, decided by a comparison already in hand.
			//
			// The row is BEHIND only when the replay applied an event the row had not seen. Then the
			// position genuinely moves and the advancing write is correct -- including the create case,
			// where there is no row and readAt is null.
			//
			// Otherwise the row is caught up (highest == readAt) or the erasure SHORTENED the stream
			// (highest < readAt, or null when every event is a tombstone). In all three the position is
			// already correct and it is the STATE that changed. Advancing cannot express that: the store
			// requires the new position to exceed the stored one, so a caught-up row is refused and the
			// refusal reads as "someone is ahead, you are done" -- which is how an erasure remedy came to
			// report success while writing nothing.
			var advancing = replayed.HighestPosition is { } h && (readAt is not { } at || h > at);

			if (advancing)
			{
				var advanced = await positioned
					.UpsertAtPositionAsync(
						aggregateId, replayed.State, readAt, replayed.HighestPosition!.Value, cancellationToken)
					.ConfigureAwait(false);

				if (PositionedProjectionWriter<TProjection>.IsSettled(
						advanced, replayed.HighestPosition!.Value))
				{
					// Settled covers three endings, and all three mean STOP. The write landed; or the
					// row is gone, which is how erasure removes personal data and re-folding would
					// reinstate it; or a live processor is already at or beyond the position this
					// replay reached, so it holds everything this replay would have written.
					LogRecovered(aggregateId, replayed.EventCount, typeof(TProjection).Name);
					return;
				}
			}
			else
			{
				// Re-fold AT readAt, never at the highest surviving position. The row has folded
				// everything up to readAt; after the erasure "everything up to readAt" is a different,
				// smaller set. The position is still an honest statement about which prefix the state
				// covers. Writing the lower highest instead would retreat the position and make the row
				// look behind, so the next batch would re-fold events it already has.
				var refolded = await positioned
					.RefoldAtPositionAsync(aggregateId, replayed.State, readAt!.Value, cancellationToken)
					.ConfigureAwait(false);

				switch (refolded.Outcome)
				{
					case ProjectionRefoldOutcome.Applied:
					case ProjectionRefoldOutcome.Vanished:
						// Written, or the row is gone because erasure deleted it. Either way the
						// subject's data is not in the projection, which is what recovery is for.
						LogRecovered(aggregateId, replayed.EventCount, typeof(TProjection).Name);
						return;

					case ProjectionRefoldOutcome.RequiresRebuild:
						// Terminal. The row carries no position to match, so nothing about it will
						// change on its own and retrying is futile.
						throw new InvalidOperationException(
							$"Recovery of projection '{typeof(TProjection).Name}' for aggregate "
							+ $"'{aggregateId}' cannot be placed: the stored row carries no established "
							+ "position, so there is no prefix for the re-folded state to be written "
							+ "against. Rebuild the projection.");

					default:
						// Superseded, and for a RE-FOLD that is NEVER settled whatever position the
						// store holds. A writer that is ahead folded from the PRE-ERASURE stream, so
						// being ahead is precisely the condition under which the erased subject's data
						// has been reintroduced. Replay again against what the row holds now.
						break;
				}
			}

			// Refused, and the store is BEHIND what we tried to write -- another writer moved the row
			// between the read and the write. Replay again from the position it now holds.
			if (attempt >= MaxRecoveryAttempts)
			{
				throw new InvalidOperationException(
					$"Recovery of projection '{typeof(TProjection).Name}' for aggregate '{aggregateId}' "
					+ $"was refused {MaxRecoveryAttempts} times because another writer advanced the "
					+ "projection during each replay. The projection is not corrupt and no data was "
					+ "lost; recovery could not obtain a consistent window. Retry when the projection's "
					+ "processor is not actively writing it.");
			}
		}
	}

	/// <summary>Replays an aggregate's whole history into fresh projection state.</summary>
	/// <returns>The folded state, the highest position folded, and how many events were applied.</returns>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; projection recovery requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	private async Task<(TProjection State, long? HighestPosition, int EventCount)> ReplayAsync<TProjection>(
		MultiStreamProjection<TProjection> projection,
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		// Load all events for this aggregate using the caller-provided aggregate type
		var storedEvents = await _eventStore.LoadAsync(
			aggregateId,
			aggregateType,
			cancellationToken).ConfigureAwait(false);

		// Create fresh projection state
		var state = new TProjection();
		long? highest = null;

		// Deserialize and apply all events through the same handlers
		foreach (var storedEvent in storedEvents)
		{
			// An erased (GDPR-tombstoned) event carries the reserved marker in place of its type and a nulled
			// payload, so no serializer can resolve it. Recognize it STRUCTURALLY, before any deserialization
			// attempt, and skip it. Recovering a fully erased aggregate therefore rebuilds its projection to
			// the empty initial state, carrying none of the subject's data forward -- rather than throwing and
			// leaving the projection permanently unrecoverable. Only the reserved marker is skipped.
			if (ErasedEventMarker.IsErased(storedEvent.EventType) || storedEvent.EventData is null)
			{
				continue;
			}

			var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
			var domainEvent = _eventSerializer.DeserializeEvent(storedEvent.EventData, eventType);

			// Recovery replays an aggregate's history, so isReplay is true and the identity is the
			// aggregate being recovered -- a recovered projection that lost its id is not recovered.
			// ApplyAsync, not Apply -- the synchronous overload cannot dispatch an asynchronous handler
			// and returned false, which this call site discarded. That matters more here than in the
			// rebuild: ReapplyAsync is the remedy the guarantee document names for clearing an erased
			// subject from a per-aggregate projection, so a GDPR erasure remedy was itself unable to fold
			// the events of any projection using WhenHandledBy.
			_ = await projection.ApplyAsync(
					state,
					domainEvent,
					new ProjectionContext(isReplay: true, storedEvent.GlobalPosition, aggregateId),
					new ProjectionHandlerContext(
						aggregateId,
						storedEvent.AggregateType,
						storedEvent.Version,
						storedEvent.Timestamp,
						isReplay: true),
					_serviceProvider,
					cancellationToken)
				.ConfigureAwait(false);

			if (highest is null || storedEvent.GlobalPosition > highest)
			{
				highest = storedEvent.GlobalPosition;
			}
		}

		return (state, highest, storedEvents.Count);
	}

	private void LogRecovered(string aggregateId, int eventCount, string projectionType) =>
		_logger.LogInformation(
			"Successfully recovered projection '{ProjectionType}' for aggregate '{AggregateId}' " +
			"by replaying {EventCount} events.",
			projectionType,
			aggregateId,
			eventCount);
}
