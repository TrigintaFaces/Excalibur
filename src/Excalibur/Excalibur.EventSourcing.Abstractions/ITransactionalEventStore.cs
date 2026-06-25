// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// <see cref="Implementation.EventSourcedRepository{TAggregate,TKey}"/> appends events and stages the
/// resulting integration messages in one atomic unit of work.
/// </para>
/// <para>
/// <b>Store-owned unit of work.</b> Unlike a "hand out a raw transaction" design, the store owns the
/// connection and transaction lifetime end to end. The caller supplies a <c>stageOutbox</c>
/// callback that enlists the outbox writes on the <em>same</em> transaction the store uses for the
/// append. Because the transaction never escapes the store, appending events and staging outbox rows
/// on two different transactions is structurally impossible — the atomicity guarantee cannot be
/// accidentally broken by a caller.
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
	/// The store opens one connection and one transaction, performs the optimistic-concurrency
	/// version check, appends the events, invokes <paramref name="stageOutbox"/> on the same
	/// transaction, then commits. On a concurrency conflict the store rolls back and does
	/// <b>not</b> invoke <paramref name="stageOutbox"/>. On any failure (a conflict or a throw from
	/// <paramref name="stageOutbox"/>) the entire transaction is rolled back, so neither the events
	/// nor the outbox rows persist. The store owns the connection and transaction lifetime
	/// (begin/commit/rollback/dispose).
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for a new aggregate).</param>
	/// <param name="stageOutbox">
	/// A callback that stages outbox messages on the supplied transaction. It is invoked after a
	/// successful append and before commit, and only when the version check succeeds.
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
