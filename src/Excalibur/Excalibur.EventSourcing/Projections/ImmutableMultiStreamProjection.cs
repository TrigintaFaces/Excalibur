// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Dispatch table for immutable projections. Maps event types to factory, transform,
/// or DI-resolved handler entries that produce new projection instances.
/// </summary>
/// <typeparam name="TProjection">The immutable projection state type.</typeparam>
internal sealed class ImmutableMultiStreamProjection<TProjection>
	where TProjection : class
{
	private readonly Dictionary<Type, ImmutableProjectionHandlerEntry> _handlers = [];

	/// <summary>
	/// Gets the event types this projection handles.
	/// </summary>
	internal IReadOnlyCollection<Type> HandledEventTypes => _handlers.Keys;

	/// <summary>
	/// Registers a factory handler (WhenCreating) that creates a new projection from an event.
	/// </summary>
	internal void AddCreatingHandler<TEvent>(Func<TEvent, TProjection> factory)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ImmutableProjectionHandlerEntry(
			CreatingFactory: (domainEvent) => factory((TEvent)domainEvent),
			TransformingFunc: null,
			AsyncHandler: null);
	}

	/// <summary>
	/// Registers a transform handler (WhenTransforming) that produces a new projection from current + event.
	/// </summary>
	internal void AddTransformingHandler<TEvent>(Func<TProjection, TEvent, TProjection> transform)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ImmutableProjectionHandlerEntry(
			CreatingFactory: null,
			TransformingFunc: (current, domainEvent) => transform(current, (TEvent)domainEvent),
			AsyncHandler: null);
	}

	/// <summary>
	/// Registers a DI-resolved async handler.
	/// </summary>
	internal void AddAsyncHandler<TEvent>(
		Func<TProjection?, IDomainEvent, ProjectionHandlerContext, IServiceProvider, CancellationToken, Task<TProjection>> handler)
		where TEvent : IDomainEvent
	{
		_handlers[typeof(TEvent)] = new ImmutableProjectionHandlerEntry(
			CreatingFactory: null,
			TransformingFunc: null,
			AsyncHandler: handler);
	}

	/// <summary>
	/// Gets the handler entry for the specified event type.
	/// </summary>
	internal ImmutableProjectionHandlerEntry? GetHandler(Type eventType)
	{
		return _handlers.TryGetValue(eventType, out var entry) ? entry : null;
	}

	/// <summary>
	/// Gets whether any async handlers are registered.
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
	/// Represents a single handler entry in the immutable projection dispatch table.
	/// Exactly one of the three delegate fields is non-null.
	/// </summary>
	internal readonly record struct ImmutableProjectionHandlerEntry(
		Func<IDomainEvent, TProjection>? CreatingFactory,
		Func<TProjection, IDomainEvent, TProjection>? TransformingFunc,
		Func<TProjection?, IDomainEvent, ProjectionHandlerContext, IServiceProvider, CancellationToken, Task<TProjection>>? AsyncHandler);
}
