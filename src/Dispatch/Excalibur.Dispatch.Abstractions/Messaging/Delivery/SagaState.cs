// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Base class for saga state management, providing fundamental properties for workflow persistence and tracking. This abstract class serves
/// as the foundation for all saga state implementations, ensuring consistent identity management and completion tracking across different
/// workflow types.
/// </summary>
public abstract class SagaState
{
	/// <summary>
	/// Maximum number of processed event IDs to retain before removing oldest entries.
	/// Prevents unbounded growth for long-running sagas.
	/// </summary>
	private const int MaxProcessedEventIds = 1000;

	/// <summary>
	/// Gets or sets the unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </summary>
	/// <value>
	/// The unique identifier for this saga instance. This identifier is used for saga correlation, state persistence, and
	/// event routing throughout the workflow lifecycle.
	/// </value>
	public Guid SagaId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets a value indicating whether this saga workflow has completed successfully. When set to true, the saga will not process
	/// further events and may be eligible for cleanup operations.
	/// </summary>
	/// <value>The current <see cref="Completed"/> value.</value>
	public bool Completed { get; set; }

	/// <summary>
	/// Gets or sets the instant at which this saga reached its terminal (completed) state, or
	/// <see langword="null"/> while the saga is still running. Set exactly once, on the terminal transition,
	/// by the saga coordination layer using its injected <see cref="System.TimeProvider"/> — never by
	/// persistence code and never from <c>DateTimeOffset.UtcNow</c>.
	/// </summary>
	/// <value>
	/// The UTC completion instant used to drive retention/purge policies (see
	/// <see cref="ISagaStore.PurgeCompletedBeforeAsync"/>), or <see langword="null"/> when the saga has not
	/// yet completed.
	/// </value>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the tenant that owns this saga instance, binding the saga's state to its originating tenant
	/// across persistence and replay.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>The strength of that binding differs per store, and this property does not tell you which you
	/// have.</strong> A populated value means the owning tenant was recorded; it does not by itself mean the
	/// store refuses a cross-tenant read. There are two shapes, and the difference is material rather than
	/// cosmetic — under the second, the row leaves the database and enters this process before it is rejected,
	/// which changes what is true for compliance, for memory, and for a crash dump.
	/// </para>
	/// <para>
	/// <strong>Server-side</strong> — a tenant's read cannot <em>retrieve</em> another tenant's row: the tenant
	/// term is in the query the database evaluates. This applies to the relational stores and to MongoDB, whose
	/// keyed reads and version-gated writes both compose a tenant equality filter.
	/// </para>
	/// <para>
	/// <strong>Client-side</strong> — a tenant's read cannot <em>return</em> another tenant's row; the row is
	/// retrieved and discarded before it reaches the caller. This applies to Cosmos DB and Firestore, whose
	/// point reads address a document by identifier and cannot carry a predicate, and to the read path of
	/// DynamoDB. It is a stated gap, not a design preference.
	/// </para>
	/// <para>
	/// <strong>The write direction is not implied by the read direction, so it is stated separately.</strong>
	/// A cross-tenant overwrite is refused on every store, but by different means: MongoDB by a server-side
	/// match on tenant, identifier and version; DynamoDB's update path by a server-side conditional expression
	/// the database itself evaluates; Firestore by an ownership comparison performed <em>inside</em> its
	/// transaction, so the check and the write are atomic; Cosmos DB by an ownership comparison after the
	/// existing document is read and before any write is issued — which is correct as written but depends on
	/// that comparison, not on the engine.
	/// </para>
	/// <para>
	/// Do not treat a populated value as a substitute for scoping at your own boundary on the client-side
	/// stores. See the saga architecture guarantee contract for the per-provider statement and the conformance
	/// arms that enforce it.
	/// </para>
	/// </remarks>
	/// <value>The owning tenant identifier, or <see langword="null"/> when the saga is not tenant-scoped.</value>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the optimistic concurrency version for this saga state. Incremented on each successful save operation.
	/// Used by <see cref="ISagaStore"/> implementations and SagaManager to detect concurrent modifications and prevent
	/// silent overwrites when multiple event handlers process events for the same saga instance simultaneously.
	/// </summary>
	/// <value>The current concurrency version of the saga state. Starts at 0 for new sagas.</value>
	public long Version { get; set; }

	/// <summary>
	/// Gets the set of event IDs that have been processed by this saga instance.
	/// Used for idempotent replay protection: if the process crashes between HandleAsync and SaveAsync,
	/// or if the same event is delivered concurrently, already-processed events are safely skipped.
	/// </summary>
	/// <value>The set of processed event identifiers, in insertion order.</value>
	/// <remarks>
	/// A bounded, best-effort <b>in-memory</b> recent-event idempotency guard (capacity
	/// <see cref="MaxProcessedEventIds"/>): when full, the <b>oldest</b> id is evicted (FIFO), and the
	/// insertion order is preserved across serialization. Durable exactly-once dedup is the inbox store's
	/// responsibility, not this guard.
	/// </remarks>
	public ISet<string> ProcessedEventIds { get; } = new BoundedProcessedEventIdSet(MaxProcessedEventIds);

	/// <summary>
	/// Attempts to mark an event as processed. Returns <see langword="false"/> if the event was already processed.
	/// When the guard is at capacity, adding a new id evicts the oldest one (FIFO) — see
	/// <see cref="ProcessedEventIds"/>.
	/// </summary>
	/// <param name="eventId">The unique event identifier.</param>
	/// <returns><see langword="true"/> if the event was newly added; <see langword="false"/> if already processed.</returns>
	public bool TryMarkEventProcessed(string eventId) => ProcessedEventIds.Add(eventId);
}
