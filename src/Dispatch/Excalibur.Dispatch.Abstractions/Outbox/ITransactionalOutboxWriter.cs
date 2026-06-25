// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

namespace Excalibur.Dispatch;

/// <summary>
/// Extends outbox staging with external transaction participation for atomic consistency.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface when the outbox store must participate in an externally owned
/// database transaction. This is required for event sourcing scenarios where the event
/// append and outbox write must be atomic -- without it, a crash between the two writes
/// causes either lost messages or phantom messages.
/// </para>
/// <para>
/// Only relational providers that support <see cref="IDbTransaction"/> (SQL Server, PostgreSQL)
/// should implement this interface. NoSQL providers (CosmosDB, DynamoDB, Firestore, Redis,
/// MongoDB, Elasticsearch) should NOT implement it.
/// </para>
/// <para>
/// <strong>Usage (store-owned unit of work — the canonical event-sourcing flow):</strong>
/// The transactional event store owns the connection and transaction end to end. Inside
/// <c>ITransactionalEventStore.AppendWithOutboxStagingAsync</c> it opens one transaction,
/// appends the events, then invokes a <c>stageOutbox</c> callback that calls
/// <see cref="StageMessageAsync"/> on that <em>same</em> store-owned transaction, and finally
/// commits (or rolls back on any failure). The caller never begins or commits the transaction.
/// <code>
/// // The store invokes this callback on its own transaction, between append and commit:
/// await transactionalWriter.StageMessageAsync(outboxMessage, transaction, cancellationToken);
/// </code>
/// </para>
/// <para>
/// <strong>Interface hierarchy:</strong>
/// <list type="bullet">
/// <item><see cref="IOutboxStore"/> -- Core polling-based outbox (stage, get unsent, mark sent/failed)</item>
/// <item><see cref="IOutboxStoreAdmin"/> -- Admin operations (failed retrieval, scheduled messages, cleanup, stats)</item>
/// <item><see cref="IOutboxStoreBatch"/> -- Batch mark sent/failed operations</item>
/// <item><see cref="ITransactionalOutboxStore"/> -- Atomic batch mark-sent within a single transaction</item>
/// <item><see cref="IMultiTransportOutboxStore"/> -- Per-transport delivery tracking for fan-out scenarios</item>
/// <item><see cref="ITransactionalOutboxWriter"/> -- External transaction participation for atomic staging (this interface)</item>
/// </list>
/// </para>
/// </remarks>
public interface ITransactionalOutboxWriter
{
	/// <summary>
	/// Stages a message within an externally owned database transaction.
	/// </summary>
	/// <param name="message">The outbound message to stage. Must not be null.</param>
	/// <param name="transaction">
	/// The database transaction to enlist the staging write on. Must not be null.
	/// The transaction owner (the transactional event store, in the canonical flow) controls its
	/// lifecycle (begin, commit, rollback); this method only stages the message on the supplied transaction.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous staging operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="transaction"/> is null.
	/// </exception>
	ValueTask StageMessageAsync(
		OutboundMessage message,
		IDbTransaction transaction,
		CancellationToken cancellationToken);
}
