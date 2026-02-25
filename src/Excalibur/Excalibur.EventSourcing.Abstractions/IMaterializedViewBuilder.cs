// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for building materialized views from events.
/// </summary>
/// <typeparam name="TView">The type of the materialized view.</typeparam>
/// <remarks>
/// <para>
/// Materialized view builders transform event streams into read-optimized projections.
/// Each builder is responsible for:
/// <list type="bullet">
/// <item>Subscribing to relevant events</item>
/// <item>Updating the view state based on events</item>
/// <item>Managing idempotent event processing</item>
/// </list>
/// </para>
/// <para>
/// Implementations should be registered in the DI container and will be
/// automatically discovered for subscription management.
/// </para>
/// <para>
/// <b>Idempotency:</b> Builders should handle duplicate event deliveries gracefully.
/// Use position tracking via <see cref="IMaterializedViewStore.GetPositionAsync"/> and
/// <see cref="IMaterializedViewStore.SavePositionAsync"/> to ensure exactly-once processing.
/// </para>
/// </remarks>
public interface IMaterializedViewBuilder<TView>
	where TView : class, new()
{
	/// <summary>
	/// Gets the name of this view builder for position tracking.
	/// </summary>
	/// <value>A unique name identifying this view builder.</value>
	string ViewName { get; }

	/// <summary>
	/// Gets the types of events this builder handles.
	/// </summary>
	/// <value>The event types this builder subscribes to.</value>
	IReadOnlyList<Type> HandledEventTypes { get; }

	/// <summary>
	/// Determines the view ID for an event.
	/// </summary>
	/// <param name="event">The event to get the view ID for.</param>
	/// <returns>The view ID, or null if the event should not update any view.</returns>
	/// <remarks>
	/// <para>
	/// This method extracts the view identifier from an event. For example,
	/// an OrderCreated event might return the OrderId as the view ID.
	/// </para>
	/// </remarks>
	string? GetViewId(IDomainEvent @event);

	/// <summary>
	/// Applies an event to the view, updating its state.
	/// </summary>
	/// <param name="view">The current view state.</param>
	/// <param name="event">The event to apply.</param>
	/// <returns>The updated view state.</returns>
	/// <remarks>
	/// <para>
	/// This method is called for each event that matches <see cref="HandledEventTypes"/>.
	/// The view passed in may be a newly created instance (if the view doesn't exist yet)
	/// or an existing view from the store.
	/// </para>
	/// </remarks>
	TView Apply(TView view, IDomainEvent @event);

	/// <summary>
	/// Creates a new instance of the view.
	/// </summary>
	/// <returns>A new view instance.</returns>
	TView CreateNew() => new();
}
