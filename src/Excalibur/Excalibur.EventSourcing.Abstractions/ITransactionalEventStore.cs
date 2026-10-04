// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Optional extension of <see cref="IEventStore"/> for providers that can append events and stage
/// outbox messages atomically within a single database transaction.
/// </summary>
/// <remarks>
/// <para>
/// Event store providers backed by a transactional database (e.g., SQL Server, Postgres) should
/// implement this interface to enable the <c>OutboxStagingStrategy.Transactional</c> path, in which
/// <see cref="IEventSourcedRepository{TAggregate, TKey}"/> appends events and stages the
/// resulting integration messages in one atomic unit of work.
/// </para>
/// <para>
/// <b>Store-owned unit of work.</b> Unlike a "hand out a raw transaction" design, the store owns the
/// connection and transaction lifetime end to end. The caller supplies a <c>stageOutbox</c>
/// callback that enlists the outbox writes on the <em>same</em> transaction the store uses for the
/// append. Atomicity requires the callback to enlist all staging writes on that supplied transaction.
/// The callback receives the transaction and must not commit it or perform independently committed
/// writes or external effects; the interface cannot enforce that discipline.
/// </para>
/// <para>
/// When the injected <see cref="IEventStore"/> does not also implement this interface, the repository
/// falls back to non-transactional behavior (events are appended and outbox staging is handled by the
/// background outbox processor).
/// </para>
/// </remarks>
public interface ITransactionalEventStore : IEventStore
{
	/// <summary>
	/// Appends events and stages outbox messages within a single atomic database transaction.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The store opens one connection and one transaction, performs the optimistic-concurrency
	/// version check, invokes <paramref name="stageOutbox"/> on the same transaction, appends the
	/// events, then commits. A failed version pre-check does not invoke <paramref name="stageOutbox"/>.
	/// A later conflict can occur after staging; a proven rollback discards both events and staged rows.
	/// Lost commit acknowledgement can leave both durable and must be reported as Unknown unless
	/// reconciliation establishes the complete operation. Callback faults before commit propagate and
	/// abort the store-owned transaction. The callback must not commit it, open independent writes or
	/// perform external effects; the supplied transaction does not enforce that discipline by itself.
	/// </para>
	/// <para>
	/// <b>Staging runs before the events are appended, and the callback must not depend on anything
	/// the append produces.</b> A provider with a global event position allocates it from a shared
	/// counter whose lock is held until commit, so every appender in the process is blocked for as
	/// long as that transaction runs. Staging is one round trip per message, so performing it after
	/// the append would place all of those round trips inside that window. The ordering is therefore
	/// a throughput property, not a correctness one — atomicity is identical either way — and it is
	/// only available because the callback receives the transaction and nothing else. Do not widen
	/// the callback to expose the assigned version or position: that would force staging back inside
	/// the window and reduce sustained append throughput for every consumer of the provider.
	/// </para>
	/// <para>
	/// <b>A concurrency conflict is classified the same way whether it is caught by the in-transaction
	/// version pre-check or lost to a genuine race that slips past it</b> (two callers both pass the
	/// pre-check, then race at insert/commit — the database's own uniqueness constraint on the stream
	/// key decides the loser). Either shape yields
	/// <see cref="AppendResult.CreateConcurrencyConflict(long, long?)"/>, matching
	/// <see cref="IEventStore.AppendAsync"/>'s contract on the same provider. A changed stream version
	/// after a lost commit acknowledgement does not establish a conflict: it may include this append.
	/// An inconclusive commit outcome is <see cref="AppendOutcome.Unknown"/>. Callback faults before
	/// commit propagate as their original exceptions. Cancellation propagates as cancellation, but
	/// cancellation after commit dispatch does not establish rollback; callers must reconcile the
	/// original operation before retrying or issuing dependent effects.
	/// </para>
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for a new aggregate).</param>
	/// <param name="stageOutbox">
	/// A callback that stages outbox messages on the supplied transaction. It is invoked only when
	/// the version check succeeds, before the events are appended and before commit. It must not
	/// depend on the version or global position the append assigns; neither is available to it.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation.</returns>
	ValueTask<AppendResult> AppendWithOutboxStagingAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
		CancellationToken cancellationToken);
}
