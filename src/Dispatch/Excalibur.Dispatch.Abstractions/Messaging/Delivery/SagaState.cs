// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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

    /// <summary>Gets or sets the persisted ordinary timer-recovery incarnation.</summary>
    /// <remarks>New state gets a fresh identity; stored values survive reload and must participate in Save CAS.</remarks>
    public Guid TimerRecoveryIncarnationId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the last original ordinary timer-batch sequence.</summary>
    public long TimerRecoverySequence { get; set; }

    /// <summary>Gets or sets the original timer intents still owed after state commit.</summary>
    public SagaTimerRecoveryJournal? PendingTimerRecovery { get; set; }

    /// <summary>Gets or sets the last acknowledged original timer-batch identity.</summary>
    public Guid LastTimerRecoveryBatchId { get; set; }


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
	/// Gets or sets the business key this saga instance was correlated on, for querying and diagnosis.
	/// </summary>
	/// <value>The correlation key, or <see langword="null"/> when the instance was addressed by identifier.</value>
	/// <remarks>
	/// <para>
	/// This is NOT how an event finds its saga. Routing is by identifier, derived from the key through
	/// <see cref="SagaCorrelation.ToSagaId(string, string)"/>, so the load path needs no query and every provider can
	/// correlate without implementing one. What this carries is the key itself, which the identifier is a
	/// one-way function of and therefore cannot be recovered from.
	/// </para>
	/// <para>
	/// <b>Without it, an operator holding a business key cannot find the saga.</b> They can derive the
	/// identifier if they know the exact type name and key, and they cannot go the other way at all: given
	/// a saga in a dashboard, nothing says which order it belongs to. That is the gap this closes, and it
	/// is a diagnosis concern rather than a routing one.
	/// </para>
	/// <para>
	/// The SQL Server provider already ships a persisted computed column over <c>$.CorrelationId</c> in the
	/// state JSON, with a filtered index on it. Until this property existed that column was always null and
	/// the index permanently empty, so the migration had been shipping an index over nothing.
	/// </para>
	/// </remarks>
	public string? CorrelationId { get; set; }


	/// <summary>
	/// Gets the set of event IDs that have been processed by this saga instance.
	/// Used for idempotent replay protection: if the process crashes between HandleAsync and SaveAsync,
	/// or if the same event is delivered concurrently, already-processed events are safely skipped.
	/// </summary>
	/// <value>The set of processed event identifiers, in insertion order.</value>
	/// <remarks>
	/// <para>
	/// A bounded, best-effort recent-event idempotency guard (capacity
	/// <see cref="MaxProcessedEventIds"/>): when full, the <b>oldest</b> id is evicted (FIFO), and both
	/// the ids and their insertion order survive being persisted and reloaded. A redelivery of an id that
	/// has already been evicted <b>re-executes the step</b> — the bound is part of the contract, not an
	/// approximation of exactly-once.
	/// </para>
	/// <para>
	/// <b>This is not durable exactly-once deduplication and must not be used as one.</b> That is the
	/// inbox store's responsibility. This guard is a bounded window over recent ids belonging to a single
	/// saga instance; a saga that can process more events than the capacity above, or that needs dedup
	/// with no bound, needs the transactional inbox in front of it.
	/// </para>
	/// <para>
	/// <b>The setter exists so this survives deserialization, and it is the whole idempotency guard.</b>
	/// A get-only collection is WRITTEN by System.Text.Json and DISCARDED on read under its default
	/// Replace handling, so without a setter every durable store reloads an empty set, every
	/// <see cref="TryMarkEventProcessed"/> returns <see langword="true"/>, and a redelivered event
	/// re-executes its step. The setter copies into the bounded set rather than replacing it, so the
	/// FIFO capacity above is preserved across a round trip instead of being swapped for whatever
	/// collection the serializer happened to construct.
	/// </para>
	/// </remarks>
	public ISet<string> ProcessedEventIds
	{
		get => _processedEventIds;
		init
		{
			ArgumentNullException.ThrowIfNull(value);

			_processedEventIds.Clear();
			foreach (var eventId in value)
			{
				_ = _processedEventIds.Add(eventId);
			}
		}
	}

	private readonly BoundedProcessedEventIdSet _processedEventIds = new(MaxProcessedEventIds);

	/// <summary>
	/// Attempts to mark an event as processed. Returns <see langword="false"/> if the event was already processed.
	/// When the guard is at capacity, adding a new id evicts the oldest one (FIFO) — see
	/// <see cref="ProcessedEventIds"/>.
	/// </summary>
	/// <param name="eventId">The unique event identifier.</param>
	/// <returns><see langword="true"/> if the event was newly added; <see langword="false"/> if already processed.</returns>
	public bool TryMarkEventProcessed(string eventId) => ProcessedEventIds.Add(eventId);

	/// <summary>
	/// Whether an event has already been processed, WITHOUT recording it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The coordinator has to ask this question before it knows whether a handler will act, and it must
	/// not record anything while asking. Using <see cref="TryMarkEventProcessed"/> as the check is what
	/// caused an event to be marked processed before its handler ran -- so a handler that declined under
	/// its guard left the event recorded and permanently retired.
	/// </para>
	/// <para>
	/// A query and a command over the same set, kept separate on purpose: the mutating one records that
	/// work HAPPENED, and only the caller that saw it happen may use it.
	/// </para>
	/// </remarks>
	/// <param name="eventId">The unique event identifier.</param>
	/// <returns><see langword="true"/> if this id is already in the guard; otherwise <see langword="false"/>.</returns>
	public bool HasProcessedEvent(string eventId) => ProcessedEventIds.Contains(eventId);

	/// <summary>
	/// Gets the names of completed steps that have a compensating action still owed, oldest first.
	/// </summary>
	/// <value>The pending compensation names, in the order the steps completed.</value>
	/// <remarks>
	/// <para>
	/// <b>This has to be part of the persisted state, not an in-memory list.</b> A saga runs for hours or
	/// days across any number of processes, so the knowledge of what has been committed and still needs
	/// undoing cannot live in the instance that committed it. A compensation log held in memory is lost by
	/// the one event that makes it matter — a crash — and the work it would have undone stays done with
	/// nothing recording that it should not have.
	/// </para>
	/// <para>
	/// <b>Unbounded, deliberately, and unlike <see cref="ProcessedEventIds"/>.</b> That guard is a bounded
	/// window because evicting an old id costs a re-execution, which is recoverable. Evicting a pending
	/// compensation costs committed work that is never undone, which is not. A saga with more outstanding
	/// compensations than is reasonable is a modelling problem to surface, never a list to silently trim.
	/// </para>
	/// <para>
	/// <b>The initializer copies rather than replaces, and that is load-bearing for the same reason it is
	/// on <see cref="ProcessedEventIds"/>.</b> A get-only collection is written by System.Text.Json and
	/// then discarded on read under its default Replace handling, so without this every durable store
	/// would reload an empty log — and an empty log is indistinguishable from a saga that owes no
	/// compensation at all. That is the failure this property exists to prevent, arriving through the
	/// serializer.
	/// </para>
	/// </remarks>
	public IList<string> PendingCompensations
	{
		get => _pendingCompensations;
		init
		{
			ArgumentNullException.ThrowIfNull(value);

			_pendingCompensations.Clear();
			_pendingCompensations.AddRange(value);
		}
	}

	private readonly List<string> _pendingCompensations = [];

	/// <summary>
	/// Records that <paramref name="stepName"/> completed work that a later failure must undo.
	/// </summary>
	/// <param name="stepName">The step name whose compensating action is now owed.</param>
	/// <remarks>
	/// Call this AFTER the step's work has been performed, never before: recording it first and failing
	/// second would owe a compensation for work that never happened, and compensating work that was never
	/// done is its own defect. Duplicates are kept rather than collapsed, because two executions of a step
	/// that each committed something owe two undos.
	/// </remarks>
	public void RecordPendingCompensation(string stepName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

		_pendingCompensations.Add(stepName);
	}

	/// <summary>
	/// Takes the pending compensations in the order they must run — most recent first — and clears the log.
	/// </summary>
	/// <returns>The owed step names, newest first.</returns>
	/// <remarks>
	/// <para>
	/// Reverse order is the whole point: a later step's work frequently depends on an earlier step's, so
	/// undoing forwards can fail on a dependency that is still in place, or succeed and leave the earlier
	/// work orphaned.
	/// </para>
	/// <para>
	/// <b>It takes rather than reads.</b> The caller is handed the list and the state no longer owes it, so
	/// a compensation pass that is interrupted after persisting cannot run the same undo twice on the next
	/// load. The cost is the mirror risk — an interruption BEFORE the state is persisted re-runs the pass —
	/// which is why compensating actions carry the same idempotency obligation as handlers, stated in the
	/// consumer obligations of the saga guarantee contract.
	/// </para>
	/// </remarks>
	public IReadOnlyList<string> TakePendingCompensations()
	{
		if (_pendingCompensations.Count == 0)
		{
			return [];
		}

		var owed = new List<string>(_pendingCompensations);
		owed.Reverse();
		_pendingCompensations.Clear();
		return owed;
	}
}
