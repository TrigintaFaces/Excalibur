// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Default implementation of <see cref="IMaterializedViewProcessor"/> that routes domain events
/// to registered <see cref="IMaterializedViewBuilder{TView}"/> instances and persists updated
/// views via <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The processor builds an event-type-to-builder routing map from
/// <see cref="MaterializedViewBuilderRegistration"/> entries at construction time. When an event
/// is processed, only builders whose <see cref="IMaterializedViewBuilder{TView}.HandledEventTypes"/>
/// include the event type are invoked.
/// </para>
/// <para>
/// For catch-up and rebuild scenarios, the processor reads the global event stream via
/// <see cref="IGlobalStreamQuery"/> and replays events through the builder pipeline.
/// Position tracking uses <see cref="IMaterializedViewStore.GetPositionAsync"/> and
/// <see cref="IMaterializedViewStore.SavePositionAsync"/>.
/// </para>
/// </remarks>
internal sealed partial class MaterializedViewProcessor : IMaterializedViewProcessor
{
	private readonly IMaterializedViewStore _viewStore;

	/// <summary>
	/// The same store, narrowed to its atomic capability, or <see langword="null"/> when every registered
	/// projection is <see cref="ViewDeliverySemantics.AtLeastOnceIdempotent"/> and the store is not atomic
	/// (Elasticsearch/OpenSearch). Non-null whenever any <see cref="ViewDeliverySemantics.ExactlyOnce"/>
	/// projection is registered: such a store is required in the constructor, so the exactly-once write path
	/// can never be reached with a store that would silently degrade to at-least-once.
	/// </summary>
	private readonly IAtomicMaterializedViewStore? _atomicViewStore;
	private readonly IGlobalStreamQuery _globalStreamQuery;
	private readonly IEventSerializer _eventSerializer;
	private readonly IOptions<MaterializedViewOptions> _options;
	private readonly ILogger<MaterializedViewProcessor> _logger;
	private readonly MaterializedViewMetrics? _metrics;

	/// <summary>
	/// Maps event type -> list of (viewName, builderRegistration) for routing.
	/// </summary>
	private readonly Dictionary<Type, List<BuilderRoute>> _eventTypeRoutes;

	/// <summary>
	/// Maps viewName -> list of builderRegistrations for catch-up/rebuild by view.
	/// </summary>
	private readonly Dictionary<string, List<MaterializedViewBuilderRegistration>> _viewNameRoutes;

	/// <summary>
	/// Maps view type -> a strongly-typed accessor over the store's generic <c>GetAsync</c>/<c>SaveAsync</c>,
	/// instantiated once per view type at construction so per-event access is a plain <c>await</c> (no
	/// per-call reflection, no <see cref="ValueTask"/> double-consume).
	/// </summary>
	private readonly Dictionary<Type, ViewStoreAccessor> _viewStoreAccessors;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewProcessor"/> class.
	/// </summary>
	/// <param name="viewStore">The materialized view store for persistence and position tracking.</param>
	/// <param name="globalStreamQuery">The global stream query for reading events during catch-up/rebuild.</param>
	/// <param name="eventSerializer">The event serializer for deserializing stored events.</param>
	/// <param name="registrations">The registered materialized view builders.</param>
	/// <param name="options">The materialized view options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="metrics">Optional refresh metrics. When supplied, each catch-up records its outcome so a stalled projection is observable rather than only logged.</param>
	public MaterializedViewProcessor(
		IMaterializedViewStore viewStore,
		IGlobalStreamQuery globalStreamQuery,
		IEventSerializer eventSerializer,
		IEnumerable<MaterializedViewBuilderRegistration> registrations,
		IOptions<MaterializedViewOptions> options,
		ILogger<MaterializedViewProcessor> logger,
		MaterializedViewMetrics? metrics = null)
	{
		_viewStore = viewStore ?? throw new ArgumentNullException(nameof(viewStore));
		_globalStreamQuery = globalStreamQuery ?? throw new ArgumentNullException(nameof(globalStreamQuery));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metrics = metrics;

		ArgumentNullException.ThrowIfNull(registrations);
		var registrationList = registrations as IReadOnlyCollection<MaterializedViewBuilderRegistration> ?? registrations.ToList();

		// An exactly-once (accumulating) projection MUST have an atomic view store: the view mutation and the
		// checkpoint advance are one durable unit, else a crash replays the last event and double-counts it.
		// An at-least-once idempotent projection tolerates that replay (upsert-by-view-id), so it may run on a
		// non-atomic store (Elasticsearch/OpenSearch). Require atomicity only when at least one exactly-once
		// projection is registered; the same rule is enforced earlier at startup by
		// AtomicMaterializedViewStoreValidator, so an exactly-once projection on a non-atomic store fails fast.
		var requiresAtomic = registrationList.Any(r => r.Semantics == ViewDeliverySemantics.ExactlyOnce);
		_atomicViewStore = requiresAtomic
			? AtomicViewStoreRequirement.Require(viewStore)
			: viewStore as IAtomicMaterializedViewStore;

		_eventTypeRoutes = new Dictionary<Type, List<BuilderRoute>>();
		_viewNameRoutes = new Dictionary<string, List<MaterializedViewBuilderRegistration>>(StringComparer.Ordinal);
		_viewStoreAccessors = new Dictionary<Type, ViewStoreAccessor>();

		BuildRoutingMaps(registrationList);
	}


	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(@event);

		var eventType = @event.GetType();

		if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
		{
			// No builders handle this event type — skip silently
			return;
		}

		// Apply and checkpoint atomically per view: the view mutation and the position advance are one
		// durable unit, so a crash cannot leave the view updated while the checkpoint lags (which would
		// replay this event on restart and double-count the view).
		foreach (var route in routes)
		{
			await ApplyEventToBuilderAsync(route.Registration, @event, position, cancellationToken).ConfigureAwait(false);
		}

		LogEventProcessed(MessageNameHelper.GetName(@event.GetType()), position);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task ProcessEventsAsync(
		IEnumerable<(IDomainEvent Event, long Position)> events,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		long lastPosition = -1;
		var affectedViews = new HashSet<string>(StringComparer.Ordinal);

		foreach (var (domainEvent, position) in events)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var eventType = domainEvent.GetType();

			if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
			{
				continue;
			}

			// Each event's view mutation and checkpoint advance are one atomic durable unit (see
			// ProcessEventAsync). A view's recorded position is the last event that actually affected it;
			// the monotonic position advance means a crash-replayed event is a safe no-op.
			foreach (var route in routes)
			{
				await ApplyEventToBuilderAsync(route.Registration, domainEvent, position, cancellationToken)
					.ConfigureAwait(false);

				affectedViews.Add(GetViewName(route.Registration));
			}

			lastPosition = position;
		}

		if (lastPosition >= 0)
		{
			LogBatchProcessed(affectedViews.Count, lastPosition);
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task RebuildAsync(CancellationToken cancellationToken)
	{
		LogRebuildStarting();

		// Clear every registered view's checkpoint so nothing resumes from a stale one.
		//
		// This used to call SavePositionAsync(viewName, 0), and that could not work on ANY provider. Every
		// store enforces a monotonic advance server-side -- SQL Server by `source.Position > target.Position`
		// in its MERGE, Postgres by `WHERE position < EXCLUDED.position`, MongoDB by `$max`, and the two
		// search stores by external versioning -- so a write of zero against an existing checkpoint was
		// refused by design. The method returned void, so the refusal was invisible, and each store then
		// logged a successful save. Measured against real Elasticsearch: the write answers HTTP 409,
		// "current version [4000001] is higher than the one provided [1]", the stored position is unchanged,
		// and the rebuild reported completion regardless.
		//
		// Lowering a checkpoint is a different operation from advancing one, so it is a different member.
		foreach (var (viewName, _) in _viewNameRoutes)
		{
			await _viewStore.ResetPositionAsync(viewName, cancellationToken).ConfigureAwait(false);
		}

		// Replay the entire global stream through all builders. The applied count is not recorded here:
		// a rebuild is an explicit operator action with its own completion log, not the periodic refresh
		// whose staleness the health check tracks.
		_ = await ReplayGlobalStreamAsync(
			GlobalStreamPosition.Start,
			allBuilders: true,
			viewNameFilter: null,
			cancellationToken).ConfigureAwait(false);

		LogRebuildCompleted();
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task CatchUpAsync(string viewName, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(viewName);

		if (!_viewNameRoutes.ContainsKey(viewName))
		{
			LogViewNotFound(viewName);
			return;
		}

		// A refresh that permanently stops making progress must be OBSERVABLE, not merely logged. The
		// health check derives staleness and failure rate from these recorders, so with nothing writing to
		// them it can only ever report healthy — a projection dead for hours looks identical to one that
		// just succeeded. Recording here is what makes a stalled refresh detectable, and here is the right
		// place because the view name, the applied count and the fault are all in scope.
		var startTimestamp = Stopwatch.GetTimestamp();

		try
		{
			var lastPosition = await _viewStore.GetPositionAsync(viewName, cancellationToken)
				.ConfigureAwait(false);

			var startPosition = lastPosition.HasValue
				? new GlobalStreamPosition(lastPosition.Value, DateTimeOffset.MinValue)
				: GlobalStreamPosition.Start;

			LogCatchUpStarting(viewName, startPosition.Position);

			var eventsApplied = await ReplayGlobalStreamAsync(
				startPosition,
				allBuilders: false,
				viewNameFilter: viewName,
				cancellationToken).ConfigureAwait(false);

			_metrics?.RecordRefreshSuccess(
				viewName,
				Stopwatch.GetElapsedTime(startTimestamp),
				eventsApplied);

			LogCatchUpCompleted(viewName);
		}
		catch (OperationCanceledException)
		{
			// A host shutting down is not a refresh failure. Recording it as one would inflate the failure
			// rate every time the process stops.
			throw;
		}
		catch (Exception ex)
		{
			_metrics?.RecordRefreshFailure(
				viewName,
				Stopwatch.GetElapsedTime(startTimestamp),
				ex.GetType().Name);
			throw;
		}
	}

	/// <summary>
	/// Replays events from the global stream through the builder pipeline.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Event deserialization is inherently dynamic; materialized view processor requires runtime type resolution.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Event deserialization requires type metadata; consumers must preserve event types.")]
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<int> ReplayGlobalStreamAsync(
		GlobalStreamPosition startPosition,
		bool allBuilders,
		string? viewNameFilter,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var currentPosition = startPosition;
		var eventsApplied = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			var storedEvents = await _globalStreamQuery.ReadAllAsync(
				currentPosition,
				opts.BatchSize,
				cancellationToken).ConfigureAwait(false);

			if (storedEvents.Count == 0)
			{
				// An empty read and a later head observation are not an atomic snapshot.
				// If the observed head is ahead, this pass cannot claim complete coverage.
				// Concurrent commits may conservatively fail a pass; retry catch-up from the
				// durable applied prefix rather than reporting success or skipping to the head.
				var head = await _globalStreamQuery.GetHeadPositionAsync(cancellationToken).ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
				if (head > currentPosition.Position)
				{
					LogStoppedShortOfHead(currentPosition.Position, head);
					throw new InvalidOperationException(
						$"Materialized view replay returned an empty page at position {currentPosition.Position} "
						+ $"but observed head {head}. Completion is unproven; retry from the saved checkpoint.");
				}

				break;
			}

			foreach (var storedEvent in storedEvents)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// An erased (GDPR-tombstoned) event carries the reserved marker in place of its type and a
				// nulled payload, so no serializer can resolve it. Recognize it STRUCTURALLY, before any
				// deserialization attempt, and continue past it: a view replay that halted at the first
				// tombstone would make an erased subject's stream permanently un-replayable. It is never routed
				// to a view builder, so it cannot populate a view. Only the reserved marker is skipped.
				if (ErasedEventMarker.IsErased(storedEvent.EventType))
				{
					LogErasedEventSkipped(storedEvent.EventId, storedEvent.GlobalPosition);
					continue;
				}

				try
				{
					var eventType = _eventSerializer.ResolveType(storedEvent.EventType);
					var domainEvent = StoredEventPayload.RequireDecoded(
						_eventSerializer.DeserializeEvent(StoredEventPayload.Require(storedEvent), eventType), storedEvent);

					if (allBuilders)
					{
						// Rebuild mode: route to all matching builders
						if (_eventTypeRoutes.TryGetValue(eventType, out var routes))
						{
							foreach (var route in routes)
							{
								await ApplyEventToBuilderAsync(route.Registration, domainEvent, storedEvent.GlobalPosition, cancellationToken)
									.ConfigureAwait(false);
							}
						}
					}
					else if (viewNameFilter is not null)
					{
						// Catch-up mode: route only to builders for the specified view
						if (_eventTypeRoutes.TryGetValue(eventType, out var routes))
						{
							foreach (var route in routes)
							{
								if (string.Equals(GetViewName(route.Registration), viewNameFilter, StringComparison.Ordinal))
								{
									await ApplyEventToBuilderAsync(route.Registration, domainEvent, storedEvent.GlobalPosition, cancellationToken)
										.ConfigureAwait(false);
								}
							}
						}
					}
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// HALT at the failed event. Do NOT advance past it.
					//
					// Swallowing here used to let the checkpoint move past an event the view never applied,
					// which leaves a permanent gap: the view reads as healthy and is wrong, and nothing
					// reports it. Every other replay loop in this package already halts for exactly this
					// reason, and treats the erasure tombstone (handled structurally above) as the only
					// skip -- a deserialization failure is never read as "assume erased", because that
					// masks genuine corruption as erasure.
					//
					// An event that RESOLVES but has no registered builder is not affected: it misses the
					// routing map below and is passed over harmlessly. Only the un-interpretable event --
					// the one case where the framework cannot know whether it mattered -- stops the replay.
					LogEventProcessingError(storedEvent.EventId, storedEvent.EventType, ex);

					throw new MaterializedViewPoisonEventException(
						viewNameFilter,
						storedEvent.EventId,
						storedEvent.EventType,
						storedEvent.GlobalPosition,
						ex);
				}
			}

			// Advance position past the last event in the batch.
			//
			// This MUST be GlobalPosition, not Version. The global stream is ordered and filtered by the
			// store's identity column, and Version is the per-aggregate version -- a small number that
			// restarts at 1 for every aggregate. Checkpointing a Version against a query that reads
			// `Position >= checkpoint` resumes from an unrelated offset: the same batch is re-read
			// indefinitely (no progress, one log line per event per pass, forever) or already-applied
			// events are replayed into an accumulating view and counted twice.
			var lastEvent = storedEvents[storedEvents.Count - 1];

			if (lastEvent.GlobalPosition <= 0)
			{
				// 0 is the documented unset sentinel. A store that does not stamp it cannot support
				// resumable replay, and continuing would checkpoint a position that reads back as
				// "start from the beginning" on every restart. Stop with something the operator can act
				// on rather than spin.
				throw new InvalidOperationException(
					$"The global stream returned event '{lastEvent.EventId}' with no global position, so materialized "
					+ "view replay cannot record where it got to and would restart from the beginning every time. "
					+ "The event store's global stream query must stamp each event's global position.");
			}

			eventsApplied += storedEvents.Count;

			var newPosition = lastEvent.GlobalPosition;
			currentPosition = new GlobalStreamPosition(newPosition, lastEvent.Timestamp);

			// Save position checkpoint after each batch
			if (allBuilders)
			{
				foreach (var (viewName, _) in _viewNameRoutes)
				{
					// A refusal here is the monotonic guard working: another writer is further ahead and
					// re-applying this position would rewind it. Discarded deliberately, not by omission.
					_ = await _viewStore.SavePositionAsync(viewName, lastEvent.GlobalPosition, cancellationToken)
						.ConfigureAwait(false);
				}
			}
			else if (viewNameFilter is not null)
			{
				// A refusal here is the monotonic guard working -- see the sibling call above.
				_ = await _viewStore.SavePositionAsync(viewNameFilter, lastEvent.GlobalPosition, cancellationToken)
					.ConfigureAwait(false);
			}

			// Brief delay between batches to avoid overwhelming the store
			if (opts.BatchDelay > TimeSpan.Zero)
			{
				await Task.Delay(opts.BatchDelay, cancellationToken).ConfigureAwait(false);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return eventsApplied;
	}

	/// <summary>
	/// Applies a domain event to a builder, loading/creating the view and saving the result.
	/// </summary>
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task ApplyEventToBuilderAsync(
		MaterializedViewBuilderRegistration registration,
		IDomainEvent domainEvent,
		long position,
		CancellationToken cancellationToken)
	{
		var viewName = GetViewName(registration);
		var viewId = GetViewId(registration, domainEvent);

		if (viewId is null)
		{
			// The event matched the builder's handled types but maps to no view instance — there is no
			// view write to make atomic, yet the checkpoint must still advance so catch-up/rebuild does
			// not re-scan this no-op event forever. A crash before this point simply re-reads a no-op.
			// A refusal is the monotonic guard working; discarded deliberately.
			_ = await _viewStore.SavePositionAsync(viewName, position, cancellationToken).ConfigureAwait(false);
			return;
		}

		// The view type is known at registration time but not at compile time here; the per-view-type
		// accessor bridges to the store's generic methods without reflection.
		var viewType = registration.ViewType;

		var existingView = await GetViewFromStoreAsync(viewType, viewName, viewId, cancellationToken)
			.ConfigureAwait(false);

		var view = existingView ?? CreateNewView(registration);

		var updatedView = ApplyEvent(registration, view, domainEvent);

		// Persist the view and advance the checkpoint. An exactly-once projection commits both ATOMICALLY, so a
		// crash cannot leave the view ahead of its checkpoint and double-count on replay. An at-least-once
		// idempotent projection writes the view then advances the checkpoint separately — safe on a non-atomic
		// store because a replayed event re-applies identically (upsert-by-view-id).
		if (registration.Semantics == ViewDeliverySemantics.ExactlyOnce)
		{
			await SaveViewAndPositionToStoreAsync(viewType, viewName, viewId, updatedView, position, cancellationToken)
				.ConfigureAwait(false);
		}
		else
		{
			await SaveViewThenPositionToStoreAsync(viewType, viewName, viewId, updatedView, position, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Loads a view from the store via the per-view-type accessor (plain <c>await</c>, no reflection).
	/// </summary>
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ValueTask<object?> GetViewFromStoreAsync(
		Type viewType,
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
		=> _viewStoreAccessors[viewType].GetAsync(_viewStore, viewName, viewId, cancellationToken);

	/// <summary>
	/// Atomically saves a view and advances its position via the per-view-type accessor (plain
	/// <c>await</c>, no reflection). Only reached for an <see cref="ViewDeliverySemantics.ExactlyOnce"/>
	/// projection, for which <see cref="_atomicViewStore"/> is guaranteed non-null by the constructor gate.
	/// </summary>
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ValueTask SaveViewAndPositionToStoreAsync(
		Type viewType,
		string viewName,
		string viewId,
		object view,
		long position,
		CancellationToken cancellationToken)
		=> _viewStoreAccessors[viewType].SaveWithPositionAsync(_atomicViewStore!, viewName, viewId, view, position, cancellationToken);

	/// <summary>
	/// Saves a view, then advances its position as a separate write, via the per-view-type accessor. Used for
	/// an <see cref="ViewDeliverySemantics.AtLeastOnceIdempotent"/> projection: a crash between the two writes
	/// replays the last event on restart, which re-applies identically (upsert-by-view-id), so the two-write
	/// path is safe on a non-atomic store such as Elasticsearch or OpenSearch.
	/// </summary>
	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async ValueTask SaveViewThenPositionToStoreAsync(
		Type viewType,
		string viewName,
		string viewId,
		object view,
		long position,
		CancellationToken cancellationToken)
	{
		await _viewStoreAccessors[viewType].SaveAsync(_viewStore, viewName, viewId, view, cancellationToken).ConfigureAwait(false);

		// NOTE: this is the NON-atomic fallback -- two separate writes. A refusal of the position advance
		// here means a concurrent writer is ahead, which is the guard working; discarded deliberately.
		_ = await _viewStore.SavePositionAsync(viewName, position, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the view name from a builder registration by invoking its ViewName property.
	/// </summary>
	private static string GetViewName(MaterializedViewBuilderRegistration registration) =>
		registration.GetViewName();

	/// <summary>
	/// Gets the view ID for an event from a builder registration.
	/// </summary>
	private static string? GetViewId(MaterializedViewBuilderRegistration registration, IDomainEvent @event)
	{
		return registration.Accessor.GetViewId(registration.BuilderInstance, @event);
	}

	/// <summary>
	/// Creates a new view instance via the builder's CreateNew method.
	/// </summary>
	private static object CreateNewView(MaterializedViewBuilderRegistration registration)
	{
		return registration.Accessor.CreateNew(registration.BuilderInstance);
	}

	/// <summary>
	/// Applies an event to a view via the builder's Apply method.
	/// </summary>
	private static object ApplyEvent(
		MaterializedViewBuilderRegistration registration,
		object view,
		IDomainEvent @event)
	{
		return registration.Accessor.Apply(registration.BuilderInstance, view, @event);
	}

	/// <summary>
	/// Builds the event-type and view-name routing maps from builder registrations.
	/// </summary>
	private void BuildRoutingMaps(IEnumerable<MaterializedViewBuilderRegistration> registrations)
	{
		foreach (var registration in registrations)
		{
			var viewName = GetViewName(registration);

			// Use the strongly-typed store accessor constructed at DI registration (no runtime reflection).
			_ = _viewStoreAccessors.TryAdd(registration.ViewType, registration.Accessor);

			// Build view-name route
			if (!_viewNameRoutes.TryGetValue(viewName, out var viewRegistrations))
			{
				viewRegistrations = [];
				_viewNameRoutes[viewName] = viewRegistrations;
			}

			viewRegistrations.Add(registration);

			// Build event-type routes by reading HandledEventTypes from the builder
			var handledTypes = registration.Accessor.GetHandledEventTypes(registration.BuilderInstance);
			foreach (var eventType in handledTypes)
			{
				if (!_eventTypeRoutes.TryGetValue(eventType, out var routes))
				{
					routes = [];
					_eventTypeRoutes[eventType] = routes;
				}

				routes.Add(new BuilderRoute(viewName, registration));
			}

			LogBuilderRegistered(viewName, registration.BuilderType.Name);
		}

		LogRoutingMapsBuilt(_eventTypeRoutes.Count, _viewNameRoutes.Count);
	}

	/// <summary>
	/// Routing entry mapping a builder registration to a view name.
	/// </summary>
	private readonly record struct BuilderRoute(string ViewName, MaterializedViewBuilderRegistration Registration);

	#region Logging

	[LoggerMessage(EventSourcingEventId.ViewProcessorEventProcessed, LogLevel.Debug,
		"Materialized view processor processed event {EventType} at position {Position}")]
	private partial void LogEventProcessed(string eventType, long position);

	[LoggerMessage(EventSourcingEventId.ViewProcessorBatchProcessed, LogLevel.Debug,
		"Materialized view processor batch completed, {ViewCount} views affected, position {Position}")]
	private partial void LogBatchProcessed(int viewCount, long position);

	/// <summary>Records that the replay stopped below the head because the run was withheld.</summary>
	/// <remarks>
	/// Information rather than Debug: the default minimum level in a stock host is Information, so a
	/// Debug line here would be invisible exactly when someone needs it. Information rather than
	/// Warning because a single occurrence is the healthy signature of an append that has not
	/// committed yet, and warning on it would train an operator to ignore the one that persists.
	/// </remarks>
	/// <param name="stoppedAt">The position the replay reached.</param>
	/// <param name="head">The stream head at the time of the check.</param>
	[LoggerMessage(EventSourcingEventId.ViewProcessorStoppedShortOfHead, LogLevel.Information,
		"Materialized view replay stopped at position {StoppedAt} while the stream head is {Head}: the "
		+ "next position is absent, so the remaining run was withheld rather than exhausted. This pass "
		+ "did not catch up. A gap that persists across passes is not an in-flight append.")]
	private partial void LogStoppedShortOfHead(long stoppedAt, long head);

	[LoggerMessage(EventSourcingEventId.ViewProcessorRebuildStarting, LogLevel.Information,
		"Materialized view rebuild starting")]
	private partial void LogRebuildStarting();

	[LoggerMessage(EventSourcingEventId.ViewProcessorRebuildCompleted, LogLevel.Information,
		"Materialized view rebuild completed")]
	private partial void LogRebuildCompleted();

	[LoggerMessage(EventSourcingEventId.ViewProcessorCatchUpStarting, LogLevel.Information,
		"Materialized view catch-up starting for view {ViewName} from position {Position}")]
	private partial void LogCatchUpStarting(string viewName, long position);

	[LoggerMessage(EventSourcingEventId.ViewProcessorCatchUpCompleted, LogLevel.Information,
		"Materialized view catch-up completed for view {ViewName}")]
	private partial void LogCatchUpCompleted(string viewName);

	[LoggerMessage(EventSourcingEventId.ViewProcessorEventError, LogLevel.Error,
		"Error processing event {EventId} of type {EventType} in materialized view processor")]
	private partial void LogEventProcessingError(string eventId, string eventType, Exception ex);

	[LoggerMessage(EventSourcingEventId.ErasedEventSkipped, LogLevel.Debug,
		"Skipping erased (tombstoned) event {EventId} at global position {GlobalPosition}; advancing past it")]
	private partial void LogErasedEventSkipped(string eventId, long globalPosition);

	[LoggerMessage(EventSourcingEventId.ViewProcessorViewNotFound, LogLevel.Warning,
		"Catch-up requested for unknown view {ViewName}")]
	private partial void LogViewNotFound(string viewName);

	[LoggerMessage(EventSourcingEventId.ViewProcessorBuilderRegistered, LogLevel.Debug,
		"Materialized view builder registered: {ViewName} ({BuilderType})")]
	private partial void LogBuilderRegistered(string viewName, string builderType);

	[LoggerMessage(EventSourcingEventId.ViewProcessorRoutingMapsBuilt, LogLevel.Information,
		"Materialized view routing maps built: {EventTypeCount} event types, {ViewCount} views")]
	private partial void LogRoutingMapsBuilt(int eventTypeCount, int viewCount);

	#endregion Logging
}
