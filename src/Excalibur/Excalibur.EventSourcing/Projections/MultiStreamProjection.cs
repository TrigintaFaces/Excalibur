// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Represents a built multi-stream projection that can apply domain events to a projection state.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// This is the result of building a <see cref="IMultiStreamProjectionBuilder{TProjection}"/>.
/// It contains the stream sources, event handlers, and can apply events to a projection instance.
/// </para>
/// </remarks>
public sealed class MultiStreamProjection<TProjection>
	where TProjection : class, new()
{
	private readonly Dictionary<Type, ProjectionHandlerEntry> _handlers = [];
	private readonly List<string> _streams = [];
	private readonly List<string> _categories = [];

	/// <summary>
	/// Gets the stream identifiers this projection sources events from.
	/// </summary>
	/// <value>The source stream identifiers.</value>
	public IReadOnlyList<string> Streams => _streams;

	/// <summary>
	/// Gets the category names this projection sources events from.
	/// </summary>
	/// <value>The source category names.</value>
	public IReadOnlyList<string> Categories => _categories;

	/// <summary>
	/// Gets the event types this projection handles.
	/// </summary>
	/// <value>The handled event types.</value>
	public IReadOnlyCollection<Type> HandledEventTypes => _handlers.Keys;

	/// <summary>
	/// Adds a stream source.
	/// </summary>
	/// <param name="streamId">The stream identifier.</param>
	internal void AddStream(string streamId) => _streams.Add(streamId);

	/// <summary>
	/// Adds a category source.
	/// </summary>
	/// <param name="category">The category name.</param>
	internal void AddCategory(string category) => _categories.Add(category);

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
			handler);
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
	/// <returns><see langword="true"/> if a handler was found and executed; otherwise, <see langword="false"/>.</returns>
	public bool Apply(TProjection projection, IDomainEvent domainEvent)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(domainEvent);

		var eventType = domainEvent.GetType();
		if (_handlers.TryGetValue(eventType, out var entry) && entry.SyncAction is not null)
		{
			entry.SyncAction(projection, domainEvent);
			return true;
		}

		return false;
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
	/// Either <see cref="SyncAction"/> or <see cref="AsyncHandler"/> is set, never both.
	/// </summary>
	internal readonly record struct ProjectionHandlerEntry(
		Action<TProjection, IDomainEvent>? SyncAction,
		Func<TProjection, IDomainEvent, ProjectionHandlerContext, IServiceProvider, CancellationToken, Task>? AsyncHandler);
}
