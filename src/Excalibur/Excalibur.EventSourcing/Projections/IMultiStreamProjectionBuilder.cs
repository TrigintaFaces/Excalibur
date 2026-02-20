// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Fluent builder for configuring multi-stream projections that aggregate events from
/// multiple streams or categories into a single projection state.
/// </summary>
/// <typeparam name="TProjection">The projection state type. Must be a class with a parameterless constructor.</typeparam>
/// <remarks>
/// <para>
/// This builder follows the Microsoft-style fluent builder pattern. Use <see cref="FromStream"/>
/// and <see cref="FromCategory"/> to specify event sources, <see cref="When{TEvent}"/> to register
/// event handlers, and <see cref="Build"/> to create the projection.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// var projection = new MultiStreamProjectionBuilder&lt;OrderSummary&gt;()
///     .FromStream("Order-123")
///     .FromCategory("Payment")
///     .When&lt;OrderPlaced&gt;((state, e) =&gt; state.Status = "Placed")
///     .When&lt;PaymentReceived&gt;((state, e) =&gt; state.IsPaid = true)
///     .Build();
/// </code>
/// </para>
/// </remarks>
public interface IMultiStreamProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	/// <summary>
	/// Adds a specific stream as an event source for this projection.
	/// </summary>
	/// <param name="streamId">The stream identifier to source events from.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMultiStreamProjectionBuilder<TProjection> FromStream(string streamId);

	/// <summary>
	/// Adds a category as an event source for this projection.
	/// </summary>
	/// <param name="category">The category name to source events from.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMultiStreamProjectionBuilder<TProjection> FromCategory(string category);

	/// <summary>
	/// Registers a handler for a specific event type.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type to handle.</typeparam>
	/// <param name="handler">The handler that updates the projection state when the event occurs.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMultiStreamProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent;

	/// <summary>
	/// Builds the multi-stream projection from the configured sources and handlers.
	/// </summary>
	/// <returns>The built <see cref="MultiStreamProjection{TProjection}"/>.</returns>
	MultiStreamProjection<TProjection> Build();
}
