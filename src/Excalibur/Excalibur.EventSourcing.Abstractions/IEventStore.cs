// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the contract for event store operations supporting event sourcing patterns.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core operations for event-sourced aggregates:
/// <list type="bullet">
/// <item>Loading events for aggregate hydration</item>
/// <item>Appending events with optimistic concurrency control</item>
/// <item>For reliable event publishing, use the transactional outbox pattern</item>
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
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate in version order.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is <see langword="null"/>.
	/// </exception>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Loads events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="fromVersion">The version to start loading from (exclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate from the specified version in order.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is <see langword="null"/>.
	/// </exception>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Appends events to the store with optimistic concurrency control.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="events">The events to append. Must not be <see langword="null"/>.</param>
	/// <param name="expectedVersion">The expected current version (-1 for new aggregate).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation.</returns>
	/// <remarks>
	/// An identifier that is <see langword="null"/>, empty, or white space is a usage error, never a legitimate
	/// stream: accepting one would fabricate a stream key and write events where no reader will ever look.
	/// Every implementation therefore rejects such an argument by throwing, rather than reporting a failed
	/// <see cref="AppendResult"/> — a returned result models a domain or infrastructure outcome, not a caller
	/// defect. Implementations validate their arguments before any I/O or concurrency check is attempted.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, or <paramref name="events"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="EventBatchTooLargeException">
	/// <paramref name="events"/> contains more events than the underlying store can append atomically. The
	/// exception carries the offending count and the limit, so a caller may split the batch and retry.
	/// </exception>
	ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);

}
