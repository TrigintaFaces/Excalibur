// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Represents a built multi-stream projection that can apply domain events to a projection state.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// Built internally by <see cref="ProjectionBuilder{TProjection}"/> during projection registration.
/// Contains event handlers and can apply events to a projection instance.
/// </para>
/// </remarks>
internal sealed class MultiStreamProjection<TProjection>
	where TProjection : class, new()
{
	private readonly Dictionary<Type, ProjectionHandlerEntry> _handlers = [];
	private readonly Dictionary<Type, Func<IDomainEvent, string>> _keySelectors = [];

	/// <summary>
	/// Gets the event types this projection handles.
	/// </summary>
	/// <value>The handled event types.</value>
	public IReadOnlyCollection<Type> HandledEventTypes => _handlers.Keys;

	/// <summary>
	/// Registers a synchronous event handler.
	/// </summary>
	/// <typeparam name="TEvent">The event type.</typeparam>
	/// <param name="handler">The handler action.</param>
	internal void AddHandler<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ProjectionHandlerEntry(
			(projection, domainEvent) => handler(projection, (TEvent)domainEvent),
			SyncContextAction: null,
			AsyncHandler: null);
	}

	/// <summary>
	/// Registers a synchronous event handler that receives <see cref="ProjectionContext"/>.
	/// </summary>
	/// <typeparam name="TEvent">The event type.</typeparam>
	/// <param name="handler">The handler action with context.</param>
	internal void AddContextHandler<TEvent>(Action<TProjection, TEvent, ProjectionContext> handler)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ProjectionHandlerEntry(
			SyncAction: null,
			SyncContextAction: (projection, domainEvent, ctx) => handler(projection, (TEvent)domainEvent, ctx),
			AsyncHandler: null);
	}

	/// <summary>
	/// Registers an asynchronous DI-resolved event handler delegate.
	/// </summary>
	/// <typeparam name="TEvent">The event type.</typeparam>
	/// <param name="handler">
	/// A pre-compiled async delegate that resolves the handler from DI and invokes it.
	/// </param>
	internal void AddAsyncHandler<TEvent>(
		Func<TProjection, IDomainEvent, ProjectionHandlerContext, IServiceProvider, CancellationToken, Task> handler)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ProjectionHandlerEntry(
			SyncAction: null,
			SyncContextAction: null,
			handler);
	}

	/// <summary>
	/// Registers a key derivation function for the specified event type.
	/// When processing an event of this type, the projection instance is loaded
	/// and stored using the derived key instead of the aggregate ID.
	/// </summary>
	/// <typeparam name="TEvent">The event type to extract the key from.</typeparam>
	/// <param name="keySelector">A function that extracts the projection key from the event.</param>
	internal void AddKeySelector<TEvent>(Func<TEvent, string> keySelector)
		where TEvent : IDomainEvent
	{
		_keySelectors[typeof(TEvent)] = e => keySelector((TEvent)e);
	}

	/// <summary>
	/// Gets the key selector for the specified event type, if one is registered.
	/// </summary>
	/// <param name="eventType">The event type to look up.</param>
	/// <returns>The key selector function, or <see langword="null"/> if none is registered.</returns>
	internal Func<IDomainEvent, string>? GetKeySelector(Type eventType)
	{
		return _keySelectors.GetValueOrDefault(eventType);
	}

	/// <summary>
	/// Gets whether any key selectors are registered, indicating events may target
	/// different projection IDs based on event data.
	/// </summary>
	internal bool HasKeySelectors => _keySelectors.Count > 0;

	/// <summary>
	/// Derives the projection identifier an event folds into: the registered key selector's answer,
	/// or the aggregate the event came from.
	/// </summary>
	/// <param name="projectionEvent">The event, carrying its own aggregate identity.</param>
	/// <returns>The projection identifier to load, fold and write.</returns>
	/// <exception cref="InvalidOperationException">The derivation produced a null or empty key.</exception>
	/// <remarks>
	/// <para>
	/// <b>This exists because the derivation was written four times and three of them were wrong.</b>
	/// The live asynchronous path derived selector-or-aggregate and honored the override hatch; the
	/// synchronous-only path derived selector-or-aggregate without it; recovery always used the
	/// aggregate id; and a rebuild always used the projection TYPE NAME — a key space no read path
	/// queries, so a rebuild wrote one meaningless document and left every row a reader loads
	/// untouched. Each copy was reasonable where it was written and they did not agree, and a key that
	/// disagrees with the read path is invisible: everything succeeds and the reader sees stale data.
	/// </para>
	/// <para>
	/// The aggregate identity is taken from the EVENT, never from a batch-level context: a batch spans
	/// aggregates, so a single per-call aggregate id is wrong for every event but one.
	/// </para>
	/// </remarks>
	internal string DeriveProjectionId(ProjectionEvent projectionEvent)
	{
		var keySelector = GetKeySelector(projectionEvent.Domain.GetType());
		var projectionId = keySelector is not null
			? keySelector(projectionEvent.Domain)
			: projectionEvent.AggregateId;

		if (string.IsNullOrEmpty(projectionId))
		{
			throw new InvalidOperationException(
				keySelector is not null
					? $"The KeyedBy selector registered for event type "
						+ $"'{projectionEvent.Domain.GetType().Name}' on projection "
						+ $"'{typeof(TProjection).Name}' returned a null or empty projection id."
					: $"Event type '{projectionEvent.Domain.GetType().Name}' folding into projection "
						+ $"'{typeof(TProjection).Name}' carries no aggregate id, and no KeyedBy selector is "
						+ "registered for it, so there is no key to fold into. Register a KeyedBy selector "
						+ "for this event type.");
		}

		return projectionId;
	}

	/// <summary>
	/// Gets the handler entry for the specified event type.
	/// </summary>
	/// <param name="eventType">The event type to look up.</param>
	/// <returns>The handler entry, or <see langword="null"/> if no handler is registered.</returns>
	internal ProjectionHandlerEntry? GetHandler(Type eventType)
	{
		return _handlers.TryGetValue(eventType, out var entry) ? entry : null;
	}

	/// <summary>
	/// Applies a domain event to the projection state if a matching synchronous handler is registered.
	/// </summary>
	/// <param name="projection">The projection state to update.</param>
	/// <param name="domainEvent">The domain event to apply.</param>
	/// <param name="aggregateId">The identifier of the aggregate the event came from.</param>
	/// <returns><see langword="true"/> if a handler was found and executed; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// The aggregate identifier is required rather than defaulted. This overload previously fabricated a
	/// context with no identity, which is how a context handler could be handed nothing to stamp its
	/// projection with.
	/// </remarks>
	public bool Apply(TProjection projection, IDomainEvent domainEvent, string aggregateId)
	{
		return Apply(projection, domainEvent, new ProjectionContext(isReplay: false, globalPosition: null, aggregateId));
	}

	/// <summary>
	/// Applies a domain event to the projection state with context if a matching synchronous handler is registered.
	/// </summary>
	/// <param name="projection">The projection state to update.</param>
	/// <param name="domainEvent">The domain event to apply.</param>
	/// <param name="context">The projection processing context.</param>
	/// <returns><see langword="true"/> if a handler was found and executed; otherwise, <see langword="false"/>.</returns>
	public bool Apply(TProjection projection, IDomainEvent domainEvent, ProjectionContext context)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(domainEvent);
		ArgumentNullException.ThrowIfNull(context);

		var eventType = domainEvent.GetType();
		if (!_handlers.TryGetValue(eventType, out var entry))
		{
			return false;
		}

		if (entry.SyncAction is not null)
		{
			entry.SyncAction(projection, domainEvent);
			return true;
		}

		if (entry.SyncContextAction is not null)
		{
			entry.SyncContextAction(projection, domainEvent, context);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Applies an event, invoking a handler of ANY registered shape, including an asynchronous one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this exists beside <see cref="Apply(TProjection, IDomainEvent, ProjectionContext)"/>.</b>
	/// That overload is synchronous, so it can dispatch only the two synchronous handler shapes. An
	/// entry registered by <c>WhenHandledBy&lt;TEvent, THandler&gt;()</c> carries only an
	/// <c>AsyncHandler</c>, and the synchronous overload fell through it to <see langword="false"/> --
	/// which its callers discarded. A rebuild or a recovery of such a projection therefore folded
	/// NOTHING and reported success.
	/// </para>
	/// <para>
	/// <b>An entry with no delegate at all THROWS rather than returning false.</b> The entry record
	/// states that exactly one of the three is set, so an entry satisfying none of them is a
	/// programming error in this class, not a projection that declines to handle the event. Returning
	/// <see langword="false"/> there is what let the async shape be dropped silently, and it would let
	/// a FOURTH shape be dropped the same way the day someone adds one. Failing loudly is what makes
	/// the omission impossible to ship.
	/// </para>
	/// </remarks>
	/// <param name="projection">The projection state to fold into.</param>
	/// <param name="domainEvent">The event to apply.</param>
	/// <param name="context">The replay-aware projection context.</param>
	/// <param name="handlerContext">The handler context asynchronous handlers receive.</param>
	/// <param name="serviceProvider">Resolves an asynchronous handler's dependencies.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when a handler was found and executed; <see langword="false"/> when no
	/// handler is registered for the event type, which is a legitimate outcome.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// A handler entry exists for the event type but carries no delegate of any known shape.
	/// </exception>
	public async Task<bool> ApplyAsync(
		TProjection projection,
		IDomainEvent domainEvent,
		ProjectionContext context,
		ProjectionHandlerContext handlerContext,
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(domainEvent);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(handlerContext);
		ArgumentNullException.ThrowIfNull(serviceProvider);

		if (!_handlers.TryGetValue(domainEvent.GetType(), out var entry))
		{
			// No handler for this type. The projection does not care about this event, which is normal.
			return false;
		}

		if (entry.SyncAction is not null)
		{
			entry.SyncAction(projection, domainEvent);
			return true;
		}

		if (entry.SyncContextAction is not null)
		{
			entry.SyncContextAction(projection, domainEvent, context);
			return true;
		}

		if (entry.AsyncHandler is not null)
		{
			await entry.AsyncHandler(projection, domainEvent, handlerContext, serviceProvider, cancellationToken)
				.ConfigureAwait(false);
			return true;
		}

		throw new InvalidOperationException(
			$"The handler entry registered for event type '{domainEvent.GetType().Name}' on projection "
			+ $"'{typeof(TProjection).Name}' carries no delegate of any known shape. Exactly one of "
			+ "SyncAction, SyncContextAction or AsyncHandler must be set. This is a defect in the "
			+ "projection registration path, not in the projection.");
	}

	/// <summary>
	/// Gets whether any context-aware synchronous handlers are registered.
	/// </summary>
	internal bool HasContextHandlers
	{
		get
		{
			foreach (var entry in _handlers.Values)
			{
				if (entry.SyncContextAction is not null)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <summary>
	/// Gets whether any async handlers are registered, indicating the inline apply
	/// delegate must use the async code path.
	/// </summary>
	internal bool HasAsyncHandlers
	{
		get
		{
			foreach (var entry in _handlers.Values)
			{
				if (entry.AsyncHandler is not null)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <summary>
	/// Represents a single handler entry in the dispatch table.
	/// Exactly one of <see cref="SyncAction"/>, <see cref="SyncContextAction"/>,
	/// or <see cref="AsyncHandler"/> is set per entry.
	/// </summary>
	internal readonly record struct ProjectionHandlerEntry(
		Action<TProjection, IDomainEvent>? SyncAction,
		Action<TProjection, IDomainEvent, ProjectionContext>? SyncContextAction,
		Func<TProjection, IDomainEvent, ProjectionHandlerContext, IServiceProvider, CancellationToken, Task>? AsyncHandler);
}
