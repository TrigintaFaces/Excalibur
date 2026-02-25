// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for event store operations supporting event sourcing patterns.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core operations for event-sourced aggregates:
/// <list type="bullet">
/// <item>Loading events for aggregate hydration</item>
/// <item>Appending events with optimistic concurrency control</item>
/// <item>Outbox pattern support via undispatched event tracking</item>
/// </list>
/// </para>
/// <para>
/// For snapshot operations, use <see cref="ISnapshotStore"/>.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for synchronous completions (e.g., in-memory stores, cache hits). Callers should await the result
/// immediately and not store the ValueTask for later use.
/// </para>
/// </remarks>
public interface IEventStore
{
	/// <summary>
	/// Loads all events for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate in version order.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Loads events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="fromVersion">The version to start loading from (exclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate from the specified version in order.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Appends events to the store with optimistic concurrency control.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for new aggregate).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation.</returns>
	ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all undispatched events for outbox pattern processing.
	/// </summary>
	/// <param name="batchSize">Maximum number of events to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Undispatched events in order of creation.</returns>
	ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks an event as dispatched for outbox pattern processing.
	/// </summary>
	/// <param name="eventId">The event identifier to mark as dispatched.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken);
}
