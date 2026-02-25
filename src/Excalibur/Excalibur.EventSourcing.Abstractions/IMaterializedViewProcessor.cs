// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for processing events and updating materialized views.
/// </summary>
/// <remarks>
/// <para>
/// The processor is responsible for:
/// <list type="bullet">
/// <item>Loading views from the store</item>
/// <item>Applying events via registered builders</item>
/// <item>Saving updated views to the store</item>
/// <item>Tracking processing position</item>
/// </list>
/// </para>
/// </remarks>
public interface IMaterializedViewProcessor
{
	/// <summary>
	/// Processes an event, updating all relevant materialized views.
	/// </summary>
	/// <param name="event">The event to process.</param>
	/// <param name="position">The global position of the event for tracking.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken cancellationToken);

	/// <summary>
	/// Processes a batch of events, updating all relevant materialized views.
	/// </summary>
	/// <param name="events">The events to process with their positions.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ProcessEventsAsync(IEnumerable<(IDomainEvent Event, long Position)> events, CancellationToken cancellationToken);

	/// <summary>
	/// Rebuilds all materialized views from scratch.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// This operation clears existing view data and replays all events from the beginning.
	/// Use with caution in production as this can be a long-running operation.
	/// </para>
	/// </remarks>
	Task RebuildAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Catches up a view from its last known position to the current position.
	/// </summary>
	/// <param name="viewName">The view name to catch up.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task CatchUpAsync(string viewName, CancellationToken cancellationToken);
}
