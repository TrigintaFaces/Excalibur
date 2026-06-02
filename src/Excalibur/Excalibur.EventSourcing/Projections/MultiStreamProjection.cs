// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// <returns><see langword="true"/> if a handler was found and executed; otherwise, <see langword="false"/>.</returns>
	public bool Apply(TProjection projection, IDomainEvent domainEvent)
	{
		return Apply(projection, domainEvent, ProjectionContext.Live);
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
