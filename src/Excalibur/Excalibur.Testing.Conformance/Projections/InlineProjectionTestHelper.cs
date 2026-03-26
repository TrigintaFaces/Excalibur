// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.Testing.Conformance.Projections;

/// <summary>
/// Test utility for applying events to projections without DI or event store (R27.27).
/// Reuses the same <see cref="MultiStreamProjection{TProjection}"/> handler mechanism
/// as inline and async projections at runtime.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
public sealed class InlineProjectionTestHelper<TProjection>
	where TProjection : class, new()
{
	private readonly MultiStreamProjection<TProjection> _projection = new();

	/// <summary>
	/// Registers an event handler for the projection, matching the runtime
	/// <c>IProjectionBuilder&lt;T&gt;.When&lt;TEvent&gt;</c> API.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type.</typeparam>
	/// <param name="handler">The handler that updates the projection state.</param>
	/// <returns>This builder for fluent chaining.</returns>
	public InlineProjectionTestHelper<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);

		_projection.AddHandler(handler);
		return this;
	}

	/// <summary>
	/// Applies the given events to the projection in order, using the registered
	/// <c>When&lt;T&gt;</c> handlers. Returns the projection for fluent assertions.
	/// </summary>
	/// <param name="projection">The projection instance to update.</param>
	/// <param name="events">The events to apply in order.</param>
	/// <returns>The same <paramref name="projection"/> instance for fluent assertion chaining.</returns>
	public TProjection Apply(TProjection projection, IEnumerable<IDomainEvent> events)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(events);

		foreach (var evt in events)
		{
			_projection.Apply(projection, evt);
		}

		return projection;
	}

	/// <summary>
	/// Applies the given events to the projection in order, using the registered
	/// <c>When&lt;T&gt;</c> handlers. Returns the projection for fluent assertions.
	/// </summary>
	/// <param name="projection">The projection instance to update.</param>
	/// <param name="events">The events to apply in order.</param>
	/// <returns>The same <paramref name="projection"/> instance for fluent assertion chaining.</returns>
	public TProjection Apply(TProjection projection, params IDomainEvent[] events)
		=> Apply(projection, (IEnumerable<IDomainEvent>)events);
}

/// <summary>
/// Factory for creating <see cref="InlineProjectionTestHelper{TProjection}"/> instances.
/// </summary>
public static class InlineProjectionTestHelper
{
	/// <summary>
	/// Creates a new test helper for the specified projection type.
	/// </summary>
	/// <typeparam name="TProjection">The projection state type.</typeparam>
	/// <returns>A fluent builder for configuring handlers and applying events.</returns>
	public static InlineProjectionTestHelper<TProjection> For<TProjection>()
		where TProjection : class, new()
		=> new();
}
