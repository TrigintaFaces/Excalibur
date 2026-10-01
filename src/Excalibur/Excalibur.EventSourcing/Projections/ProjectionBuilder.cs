// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Internal implementation of <see cref="IProjectionBuilder{TProjection}"/>.
/// Builds a <see cref="ProjectionRegistration"/> and registers it in the
/// <see cref="IProjectionRegistry"/>.
/// </summary>
internal sealed class ProjectionBuilder<TProjection> : IProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	private readonly IProjectionRegistry? _registry;
	private readonly IServiceCollection? _services;
	private readonly MultiStreamProjection<TProjection> _projection = new();
	private ProjectionMode _mode = ProjectionMode.Async;
	private TimeSpan? _cacheTtl;
	private Func<string, CancellationToken, Task>? _deleteAction;
	private Type? _storeType;
	private ProjectionOptions? _options;
	private Func<TProjection, string>? _searchTextComputer;
	private Action<TProjection, string>? _searchTextSetter;

	/// <summary>
	/// Creates a builder with a registry for direct build (used by tests).
	/// </summary>
	internal ProjectionBuilder(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
	}

	/// <summary>
	/// Creates a builder with DI service collection access for handler registration.
	/// The projection is registered in the registry later via <see cref="Build(IProjectionRegistry)"/>.
	/// </summary>
	internal ProjectionBuilder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		_services = services;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> Inline()
	{
		_mode = ProjectionMode.Inline;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> Async()
	{
		_mode = ProjectionMode.Async;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> Ephemeral()
	{
		_mode = ProjectionMode.Ephemeral;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);
		_projection.AddHandler(handler);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent, ProjectionContext> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);
		_projection.AddContextHandler(handler);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WhenHandledBy<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IProjectionEventHandler<TProjection, TEvent>
	{
		// Pre-compile a delegate that resolves the handler from DI and invokes it.
		// All generics are closed at registration time -- AOT-safe, no reflection on hot path.
		_projection.AddAsyncHandler<TEvent>(
			async (projection, domainEvent, context, serviceProvider, cancellationToken) =>
			{
				var handler = (IProjectionEventHandler<TProjection, TEvent>)
					serviceProvider.GetRequiredService(typeof(THandler));
				await handler.HandleAsync(projection, (TEvent)domainEvent, context, cancellationToken)
					.ConfigureAwait(false);
			});

		// Register the handler type in DI if IServiceCollection is available (T.6)
		_services?.TryAddTransient(typeof(THandler));

		return this;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IProjectionEventHandler<T, TEvent> implementations.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Assembly scanning uses MakeGenericMethod; consumers should use explicit registration for AOT scenarios.")]
	public IProjectionBuilder<TProjection> AddProjectionHandlersFromAssembly(Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerInterfaceType = typeof(IProjectionEventHandler<,>);
		var projectionType = typeof(TProjection);

		// Track which event types have handlers for duplicate detection (D3)
		var discoveredHandlers = new Dictionary<Type, Type>();

		foreach (var type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface || !type.IsClass || type.IsGenericTypeDefinition)
			{
				continue;
			}

			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != handlerInterfaceType)
				{
					continue;
				}

				var genericArgs = iface.GetGenericArguments();
				var handlerProjectionType = genericArgs[0];
				var eventType = genericArgs[1];

				// Only register handlers for this projection type
				if (handlerProjectionType != projectionType)
				{
					continue;
				}

				// Duplicate detection (D3): InvalidOperationException on same (TProjection, TEvent)
				if (discoveredHandlers.TryGetValue(eventType, out var existingHandler))
				{
					throw new InvalidOperationException(
						$"Duplicate handler for ({projectionType.Name}, {eventType.Name}): " +
						$"both {existingHandler.Name} and {type.Name} handle the same event type. " +
						$"Only one handler per (TProjection, TEvent) pair is allowed.");
				}

				discoveredHandlers[eventType] = type;

				// Register via reflection: call the private generic method with closed types
				var registerMethod = typeof(ProjectionBuilder<TProjection>)
					.GetMethod(nameof(RegisterScannedHandler), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(eventType, type);

				registerMethod.Invoke(this, null);
			}
		}

		return this;
	}

	/// <summary>
	/// Registers a scanned handler type via the typed WhenHandledBy path.
	/// Called via reflection during assembly scanning with closed generic types.
	/// </summary>
	[RequiresUnreferencedCode("Called via reflection during assembly scanning.")]
	private void RegisterScannedHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IProjectionEventHandler<TProjection, TEvent>
	{
		WhenHandledBy<TEvent, THandler>();
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> KeyedBy<TEvent>(Func<TEvent, string> keySelector)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(keySelector);
		_projection.AddKeySelector(keySelector);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl)
	{
		_cacheTtl = ttl;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WhenDeleted(Func<string, CancellationToken, Task> deleteAction)
	{
		ArgumentNullException.ThrowIfNull(deleteAction);
		_deleteAction = deleteAction;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithStore<TStore>()
		where TStore : class, IProjectionStore<TProjection>
	{
		_storeType = typeof(TStore);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithOptions(Action<ProjectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_options ??= new ProjectionOptions();
		configure(_options);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithSearchText(
		Func<TProjection, string> computeSearchText,
		Action<TProjection, string> setSearchText)
	{
		ArgumentNullException.ThrowIfNull(computeSearchText);
		ArgumentNullException.ThrowIfNull(setSearchText);
		_searchTextComputer = computeSearchText;
		_searchTextSetter = setSearchText;
		return this;
	}

	/// <summary>
	/// Gets the optional cache TTL configured via <see cref="WithCacheTtl"/>.
	/// </summary>
	internal TimeSpan? CacheTtl => _cacheTtl;

	/// <summary>
	/// Gets the delete action configured via <see cref="WhenDeleted"/>.
	/// </summary>
	internal Func<string, CancellationToken, Task>? DeleteAction => _deleteAction;

	/// <summary>
	/// Gets the store type override configured via <see cref="WithStore{TStore}"/>.
	/// </summary>
	internal Type? StoreType => _storeType;

	/// <summary>
	/// Gets the projection options configured via <see cref="WithOptions"/>.
	/// </summary>
	internal ProjectionOptions? Options => _options;

	/// <summary>
	/// Gets the search text computation function configured via <see cref="WithSearchText"/>.
	/// </summary>
	internal Func<TProjection, string>? SearchTextComputer => _searchTextComputer;

	/// <summary>
	/// Gets the search text setter configured via <see cref="WithSearchText"/>.
	/// </summary>
	internal Action<TProjection, string>? SearchTextSetter => _searchTextSetter;

	/// <summary>
	/// Builds and registers the projection using the registry provided at construction.
	/// A second call for the same projection type replaces the first.
	/// </summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal void Build()
	{
		if (_registry is null)
		{
			throw new InvalidOperationException(
				"Build() requires a registry. Use Build(IProjectionRegistry) or construct with a registry.");
		}

		Build(_registry);
	}

	/// <summary>
	/// Builds and registers the projection in the specified registry.
	/// A second call for the same projection type replaces the first.
	/// </summary>
	/// <param name="registry">The projection registry to register in.</param>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal void Build(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		// Capture the generic type in a delegate at registration time (AOT-safe, no MakeGenericMethod).
		// Both Inline and Async modes need apply delegates: Inline runs during SaveAsync,
		// Async runs via the background AsyncProjectionProcessingHost.
		var inlineApply = _mode is ProjectionMode.Inline or ProjectionMode.Async
			? CreateInlineApplyDelegate()
			: null;

		// GDPR erasure clears a subject from a read model by replaying the aggregate it just tombstoned,
		// and the delegate is bound HERE because TProjection is in scope only at registration: closing
		// ReapplyAsync<T> at run time would be the MakeGenericMethod pattern this project forbids.
		//
		// BOUND ONLY WHERE IT CAN WORK, which makes the case it cannot cover INEXPRESSIBLE rather than
		// guarded. An EPHEMERAL projection persists no row, so a tombstoned stream already yields a clean
		// result and there is nothing for an erasure to miss. A KEYED projection maps many aggregates onto
		// one row, so replaying a single aggregate cannot produce a correct row for such a key — writing
		// the aggregate id would target a key no reader queries, and writing the derived key would
		// overwrite it with a state missing every other contributing aggregate. Recovery refuses that by
		// design; a null delegate is the same refusal, stated where erasure can act on it instead of
		// discovered from a thrown exception.
		var clearForAggregate =
			_mode is ProjectionMode.Inline or ProjectionMode.Async && !_projection.HasKeySelectors
				? CreateClearForAggregateDelegate()
				: null;

		// Type-erase the search text delegates for storage in the non-generic ProjectionRegistration.
		// The cast from object back to TProjection is safe because the projection engine only
		// invokes these with instances of TProjection.
		Func<object, string>? searchTextComputer = _searchTextComputer is not null
			? obj => _searchTextComputer((TProjection)obj)
			: null;
		Action<object, string>? searchTextSetter = _searchTextSetter is not null
			? (obj, text) => _searchTextSetter((TProjection)obj, text)
			: null;

		var registration = new ProjectionRegistration(
			typeof(TProjection),
			_mode,
			_projection,
			inlineApply,
			_cacheTtl,
			_deleteAction,
			_storeType,
			_options,
			searchTextComputer,
			searchTextSetter,
			clearForAggregate);

		registry.Register(registration);
	}

	/// <summary>
	/// Builds the delegate that clears one erased aggregate's contribution from this projection.
	/// </summary>
	/// <remarks>
	/// Closed over <typeparamref name="TProjection"/> at registration time, so the recovery call is an
	/// ordinary generic invocation rather than a reflective one.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static ProjectionRegistration.ClearForAggregateDelegate CreateClearForAggregateDelegate() =>
		static async (serviceProvider, aggregateId, aggregateType, cancellationToken) =>
		{
			// NO ROW MEANS NOTHING TO CLEAR, and this read is what stops a compliance operation WRITING to
			// a read model it was only asked to clear.
			//
			// Recovery CREATES a row when none exists — that is its primary documented job, repairing an
			// inline projection that failed after its events were committed — so invoking it unconditionally
			// would materialize an empty row in every persisted projection that never held this aggregate.
			// A consumer's GetByIdAsync would then return a default-valued projection where it returned
			// null, which is a read-model change caused by erasing an unrelated subject.
			//
			// The check belongs here and NOT inside ReapplyAsync, whose create-the-missing-row behaviour is
			// correct for the caller it was written for. It also skips the replay entirely for a projection
			// that never saw the subject, which is the common case on a host with several read models.
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();

			if (await store.GetByIdAsync(aggregateId, cancellationToken).ConfigureAwait(false) is null)
			{
				return;
			}

			await serviceProvider.GetRequiredService<IProjectionRecovery>()
				.ReapplyAsync<TProjection>(aggregateId, aggregateType, cancellationToken)
				.ConfigureAwait(false);
		};

	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ProjectionRegistration.InlineApplyDelegate CreateInlineApplyDelegate()
	{
		var projection = _projection;
		var searchTextComputer = _searchTextComputer;
		var searchTextSetter = _searchTextSetter;

		// Fast path: when all handlers are synchronous (with or without context) and no
		// key selectors exist, use the simpler single-ID code path that avoids Dictionary
		// allocation and async overhead.
		if (!projection.HasAsyncHandlers)
		{
			return CreateSyncOnlyApplyDelegate(projection, searchTextComputer, searchTextSetter);
		}

		// Full path: supports both sync and async handlers, multi-ID via KeyedBy and OverrideProjectionId (D1)
		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);

			// The handler context is built PER EVENT inside the loop below, not once here: a batch can
			// span aggregates, so there is no single aggregate id that describes the whole call.

			// Multi-ID: lazily loaded projection instances keyed by projection ID (D1)
			var projections = new Dictionary<string, TProjection>(StringComparer.Ordinal);
			var readPositions = new Dictionary<string, long?>(StringComparer.Ordinal);
			var folded = new Dictionary<string, List<ProjectionEvent>>(StringComparer.Ordinal);

			foreach (var projectionEvent in events)
			{
				var @event = projectionEvent.Domain;
				var entry = projection.GetHandler(@event.GetType());
				if (entry is null)
				{
					continue;
				}

				var handlerEntry = entry.Value;

				// ONE key derivation, shared with the synchronous path, recovery and rebuild. It used to
				// be written out here and three more times, and the copies did not agree.
				var projectionId = projection.DeriveProjectionId(projectionEvent);

				// Read BEFORE filtering, and that order is the whole point. The filter compares this
				// event's position against the position the projection was READ at, so the read has to
				// have happened first. Filtering first leaves the map empty for the FIRST event of every
				// projection id, which then escapes the filter and is folded a second time -- and that
				// is not a rare interleaving, it happens on every restart, because the subscription
				// checkpoint and the projection's own position advance independently.
				//
				// Loading here cannot create a ghost projection: an event with no handler already
				// continued above, so every event reaching this line is one this projection handles.
				_ = await GetOrLoadAsync(projections, readPositions, store, positioned, projectionId, cancellationToken)
					.ConfigureAwait(false);

				// Has THIS id already folded this event? Without the answer a redelivered batch
				// recomputes the same state, the store refuses it as non-advancing, and the reader
				// stalls on the overlap forever.
				//
				// IT DISCARDS THE FOLD. IT DOES NOT SKIP THE DELIVERY, and that distinction is the
				// whole of the second defect here. This used to `continue`, which is correct for the
				// primary id and silently wrong for an OVERRIDE id: an event can be owed to two ids
				// holding different stored positions, and skipping the delivery outright means the
				// override never learns the event existed. A later event in the same batch then
				// advances the override past it and nothing re-reads it -- permanent omission, no
				// upper bound, no signal. The override id is outside this predicate's domain
				// entirely, so the predicate was answering the wrong question.
				var primaryAlreadyFolded =
					positioned is not null
					&& readPositions.TryGetValue(projectionId, out var alreadyAt)
					&& PositionedProjectionWriter<TProjection>.AlreadyFolded(
						alreadyAt, projectionEvent.GlobalPosition);

				// A SYNCHRONOUS handler cannot set an override: the override id is read off the
				// ProjectionHandlerContext, and neither synchronous shape is handed one. So for a
				// synchronous handler there is nothing left to discover and the skip stays a skip --
				// it costs nothing and reaches nothing. The delivery below is only ever paid on the
				// asynchronous path, which is the only path that can route an event to a second id.
				if (primaryAlreadyFolded && handlerEntry.AsyncHandler is null)
				{
					continue;
				}

				// Built PER EVENT: the aggregate identity describes the event being applied, and a batch
				// spanning aggregates has no single answer for it. Constructing it here also removes the
				// need to reset OverrideProjectionId, which a reused instance carried between events.
				var handlerContext = new ProjectionHandlerContext(
					projectionEvent.AggregateId,
					context.AggregateType,
					context.CommittedVersion,
					context.Timestamp,
					context.IsReplay);

				if (handlerEntry.SyncAction is not null)
				{
					var state = await GetOrLoadAsync(projections, readPositions, store, positioned, projectionId, cancellationToken)
						.ConfigureAwait(false);
					handlerEntry.SyncAction(state, @event);
					RecordFold(folded, projectionId, projectionEvent);
				}
				else if (handlerEntry.SyncContextAction is not null)
				{
					var state = await GetOrLoadAsync(projections, readPositions, store, positioned, projectionId, cancellationToken)
						.ConfigureAwait(false);
					// Carries the aggregate identity, which the event itself does not have -- the stored
					// envelope is authoritative for it. Without this a context handler cannot stamp the
					// projection with its own id, and a client that reads the projection back has no
					// identifier to send to an update command.
					handlerEntry.SyncContextAction(
						state,
						@event,
						new ProjectionContext(context.IsReplay, projectionEvent.GlobalPosition, projectionEvent.AggregateId));
					RecordFold(folded, projectionId, projectionEvent);
				}
				else if (handlerEntry.AsyncHandler is not null)
				{
					// A THROWAWAY instance when the primary has already folded this event. The handler
					// has to RUN for the override id to be discovered -- there is no way to learn it
					// without running it -- and it must not run against the primary's real state, which
					// already contains this event. The throwaway absorbs the fold and is dropped; the
					// primary is neither mutated nor recorded.
					//
					// The cost, stated rather than left to be found: on the overlap window only, an
					// asynchronous handler is invoked once more than it would have been. That window is
					// redelivery, not the steady state, and an asynchronous override handler is already
					// invoked twice per event (once per target). A handler whose side effects live
					// outside the projection must be idempotent, which is what IsReplay and the
					// at-least-once delivery contract already require of it.
					var state = primaryAlreadyFolded
						? new TProjection()
						: await GetOrLoadAsync(projections, readPositions, store, positioned, projectionId, cancellationToken)
							.ConfigureAwait(false);

					await handlerEntry.AsyncHandler(state, @event, handlerContext, serviceProvider, cancellationToken)
						.ConfigureAwait(false);

					if (!primaryAlreadyFolded)
					{
						RecordFold(folded, projectionId, projectionEvent);
					}

					// OverrideProjectionId escape hatch: if handler set a DIFFERENT key, re-invoke there
					if (handlerContext.OverrideProjectionId is not null
						&& !string.Equals(handlerContext.OverrideProjectionId, projectionId, StringComparison.Ordinal))
					{
						var customId = handlerContext.OverrideProjectionId;
						var customState = await GetOrLoadAsync(projections, readPositions, store, positioned, customId, cancellationToken)
							.ConfigureAwait(false);

						// THE ALREADY-FOLDED CHECK BELONGS HERE, BEFORE THE HANDLER RUNS -- not before the
						// `folded` append further down. The filter at the top of this loop is evaluated against
						// `projectionId`, but a fold can target TWO ids, and the override id is outside that
						// filter's domain entirely. It answers "has the PRIMARY already folded this event?" when
						// the load-bearing question for THIS write is "has the TARGET of this fold already
						// folded it?"
						//
						// Gating only the append would look correct and would not be. The handler MUTATES
						// customState, so a second fold of an already-folded event corrupts the cached state
						// whether or not this event is recorded in `folded`; the corruption is then persisted the
						// moment any LATER event legitimately advances customId -- under a position that is
						// itself truthful. That moves the symptom from "wrong state at this position" to "wrong
						// state at a later one", which is harder to attribute and no less wrong.
						//
						// Reachable on an ordinary overlapping batch, which is normal on restart: C stored at
						// 100, batch carries 95 and 105 both routed to C. Without this gate 95 is folded twice,
						// HighestPosition is 105 > 100 and expectedPosition is 100 = stored, so BOTH conjuncts of
						// the conditional write are satisfied and the store admits it. The position is truthful
						// and the state is not -- the conditional write has no opinion on double-folding.
						//
						// `eventPos` from the primary filter is NOT in scope here: it is declared by that
						// pattern match inside its own `if`. This needs its own match on GlobalPosition.
						var overrideAlreadyFolded =
							positioned is not null
							&& readPositions.TryGetValue(customId, out var customAt)
							&& PositionedProjectionWriter<TProjection>.AlreadyFolded(
								customAt, projectionEvent.GlobalPosition);

						if (!overrideAlreadyFolded)
						{
							await handlerEntry.AsyncHandler(customState, @event, handlerContext, serviceProvider, cancellationToken)
								.ConfigureAwait(false);

							// The overridden id folded this event too, and the write loop keys off `folded`
							// to decide what to persist and at which position. Without this the overridden
							// projection is loaded, mutated, and then skipped -- the same silent drop as the
							// save path, reached by a different route.
							RecordFold(folded, customId, projectionEvent);
						}
					}
				}
			}

			// Compute search text once per projection instance (after all events applied)
			if (searchTextComputer is not null && searchTextSetter is not null)
			{
				foreach (var (_, state) in projections)
				{
					searchTextSetter(state, searchTextComputer(state));
				}
			}

			// Upsert all projection instances that were loaded/modified (D1)
			foreach (var (id, state) in projections)
			{
				// Nothing folded into this id means nothing to record: writing here would advance its
				// position past events this call never applied.
				//
				// An EMPTY fold is not the same as an UNPOSITIONED one. This skip is for the first case;
				// the second -- the save path, where events are being committed now and carry no global
				// position yet -- still has to be written, and the writer does it unconditionally. An
				// earlier shape conflated them and silently discarded every inline projection.
				if (!folded.TryGetValue(id, out var applied) || applied.Count == 0)
				{
					continue;
				}

				_ = await PositionedProjectionWriter<TProjection>.WriteAsync(
						store,
						positioned,
						id,
						state,
						readPositions.TryGetValue(id, out var readAt) ? readAt : null,
						PositionedProjectionWriter<TProjection>.HighestPosition(applied),
						cancellationToken)
					.ConfigureAwait(false);
			}
		};

		// Records that a projection id folded this event. The write loop keys off this map to decide
		// what to persist and at which position, so an id that folded an event and did not record it is
		// loaded, mutated and then silently dropped.
		static void RecordFold(
			Dictionary<string, List<ProjectionEvent>> folded,
			string projectionId,
			ProjectionEvent projectionEvent)
		{
			if (!folded.TryGetValue(projectionId, out var foldedHere))
			{
				foldedHere = [];
				folded[projectionId] = foldedHere;
			}

			foldedHere.Add(projectionEvent);
		}

		// Loads through the POSITIONED capability when the store has one, so the state and the position
		// it was read at arrive as a single observation. Reading them separately would let a writer
		// interleave, after which the expected position certifies a prefix the state does not contain
		// and the conditional write accepts -- losing every event in the gap.
		static async Task<TProjection> GetOrLoadAsync(
			Dictionary<string, TProjection> cache,
			Dictionary<string, long?> readPositions,
			IProjectionStore<TProjection> store,
			IPositionedProjectionStore<TProjection>? positioned,
			string id,
			CancellationToken cancellationToken)
		{
			if (!cache.TryGetValue(id, out var state))
			{
				long? readAt = null;
				if (positioned is not null)
				{
					(state, var readAtPos) = await positioned.GetWithPositionAsync(id, cancellationToken)
						.ConfigureAwait(false);
					readAt = readAtPos.ExpectedPositionOrNull;
				}
				else
				{
					state = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
				}

				state ??= new TProjection();
				cache[id] = state;
				readPositions[id] = readAt;
			}

			return state;
		}
	}

	/// <summary>
	/// Creates a simplified delegate for projections that only have sync handlers.
	/// Avoids Dictionary allocation and async overhead when no key selectors are present.
	/// </summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static ProjectionRegistration.InlineApplyDelegate CreateSyncOnlyApplyDelegate(
		MultiStreamProjection<TProjection> projection,
		Func<TProjection, string>? searchTextComputer,
		Action<TProjection, string>? searchTextSetter)
	{
		// When key selectors exist, different events may target different projection IDs
		if (projection.HasKeySelectors)
		{
			return async (events, context, serviceProvider, cancellationToken) =>
			{
				var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
				var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);

				var projections = new Dictionary<string, TProjection>(StringComparer.Ordinal);
				var readPositions = new Dictionary<string, long?>(StringComparer.Ordinal);
				var folded = new Dictionary<string, List<ProjectionEvent>>(StringComparer.Ordinal);

				foreach (var projectionEvent in events)
				{
					var @event = projectionEvent.Domain;
					if (projection.GetHandler(@event.GetType()) is null)
					{
						continue;
					}

					var id = projection.DeriveProjectionId(projectionEvent);

					if (string.IsNullOrEmpty(id))
					{
						throw new InvalidOperationException(
							$"KeyedBy selector for event type {@event.GetType().Name} returned a null or empty projection ID.");
					}

					if (!projections.TryGetValue(id, out var state))
					{
						// State and position read TOGETHER when the store records one: reading them
						// separately lets a writer interleave, after which the position the caller holds
						// certifies a prefix its state does not contain and the conditional write accepts.
						long? readAt = null;
						if (positioned is not null)
						{
							(state, var readAtPos) = await positioned.GetWithPositionAsync(id, cancellationToken)
								.ConfigureAwait(false);
							readAt = readAtPos.ExpectedPositionOrNull;
						}
						else
						{
							state = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
						}

						state ??= new TProjection();
						projections[id] = state;
						readPositions[id] = readAt;
						folded[id] = [];
					}

					// Skip what this projection has already folded in. Without it a redelivered batch
					// recomputes the same state, the store refuses it as non-advancing, and the reader
					// stalls on the overlap.
					if (positioned is not null
						&& PositionedProjectionWriter<TProjection>.AlreadyFolded(
							readPositions[id], projectionEvent.GlobalPosition))
					{
						continue;
					}

					folded[id].Add(projectionEvent);

					// context.AggregateId, NOT id: with a KeyedBy selector registered, `id` is the
					// projection key (a category, tenant, date...) and is deliberately not the aggregate.
					projection.Apply(
						state,
						@event,
						new ProjectionContext(context.IsReplay, projectionEvent.GlobalPosition, projectionEvent.AggregateId));
				}

				// Compute search text once per projection instance (after all events applied)
				if (searchTextComputer is not null && searchTextSetter is not null)
				{
					foreach (var (_, state) in projections)
					{
						searchTextSetter(state, searchTextComputer(state));
					}
				}

				foreach (var (id, state) in projections)
				{
					// Nothing folded means nothing to record: writing would advance the position past
					// events this call never applied. An UNPOSITIONED fold is a different case and is
					// still written -- unconditionally, by the writer.
					if (folded[id].Count == 0)
					{
						continue;
					}

					_ = await PositionedProjectionWriter<TProjection>.WriteAsync(
							store,
							positioned,
							id,
							state,
							readPositions[id],
							PositionedProjectionWriter<TProjection>.HighestPosition(folded[id]),
							cancellationToken)
						.ConfigureAwait(false);
				}
			};
		}

		// No key selectors: the projection id IS the aggregate id.
		//
		// Keyed PER AGGREGATE rather than once per call, deliberately. A batch can span aggregates, and
		// folding a single state for the whole call would merge unrelated aggregates into one projection
		// the moment the caller stops dispatching one aggregate at a time. Keying here makes the apply
		// path independent of how the caller batches, which is what lets the batch be delivered in
		// stream order instead of grouped.
		//
		// Lazy-load is preserved per id: the store is not touched for an aggregate until an event
		// actually matches a handler, so unrelated events cannot create a ghost projection.
		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);

			var projections = new Dictionary<string, TProjection>(StringComparer.Ordinal);
			var readPositions = new Dictionary<string, long?>(StringComparer.Ordinal);
			var applied = new Dictionary<string, List<ProjectionEvent>>(StringComparer.Ordinal);

			foreach (var projectionEvent in events)
			{
				var @event = projectionEvent.Domain;
				if (projection.GetHandler(@event.GetType()) is null)
				{
					continue;
				}

				// The shared derivation, which on this path resolves to the aggregate id -- no key
				// selector is registered, or the caller would be on the keyed path above. Routed through
				// it anyway so an empty aggregate id is refused here exactly as it is everywhere else,
				// rather than silently writing a row under the empty key.
				var id = projection.DeriveProjectionId(projectionEvent);
				if (!projections.TryGetValue(id, out var state))
				{
					// When the store records positions, the state and the position are read TOGETHER.
					// Reading them separately lets a writer interleave, after which the position the
					// caller holds certifies a prefix its state does not contain -- and the conditional
					// write then ACCEPTS, losing every event in between.
					long? readAt = null;
					if (positioned is not null)
					{
						(state, var readAtPos) = await positioned.GetWithPositionAsync(id, cancellationToken)
							.ConfigureAwait(false);
						readAt = readAtPos.ExpectedPositionOrNull;
					}
					else
					{
						state = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
					}

					state ??= new TProjection();
					projections[id] = state;
					readPositions[id] = readAt;
					applied[id] = [];
				}

				// Skip what is already folded in. Without this a redelivered batch recomputes the same
				// state, the store refuses it as non-advancing, and the reader never progresses.
				if (positioned is not null
					&& PositionedProjectionWriter<TProjection>.AlreadyFolded(
						readPositions[id], projectionEvent.GlobalPosition))
				{
					continue;
				}

				projection.Apply(
					state,
					@event,
					new ProjectionContext(context.IsReplay, projectionEvent.GlobalPosition, id));

				applied[id].Add(projectionEvent);
			}

			if (searchTextComputer is not null && searchTextSetter is not null)
			{
				foreach (var (_, state) in projections)
				{
					searchTextSetter(state, searchTextComputer(state));
				}
			}

			foreach (var (id, state) in projections)
			{
				// Nothing folded means nothing to record: writing here would advance the position past
				// events this call never applied. An UNPOSITIONED fold is a different case and is still
				// written -- unconditionally, by the writer.
				if (applied[id].Count == 0)
				{
					continue;
				}

				_ = await PositionedProjectionWriter<TProjection>.WriteAsync(
						store,
						positioned,
						id,
						state,
						readPositions[id],
						PositionedProjectionWriter<TProjection>.HighestPosition(applied[id]),
						cancellationToken)
					.ConfigureAwait(false);
			}
		};
	}
}
