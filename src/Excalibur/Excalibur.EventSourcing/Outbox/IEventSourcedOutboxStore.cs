// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Transactional outbox store for reliable event publishing in event-sourced systems.
/// Provides low-level CRUD operations for outbox entries with explicit transaction support.
/// </summary>
/// <remarks>
/// <para>
/// This interface is specifically designed for event sourcing scenarios where domain events
/// must be published reliably using the outbox pattern. It differs from the messaging outbox
/// (<c>Excalibur.Dispatch.Abstractions.Outbox.IOutboxStore</c>) in that it:
/// </para>
/// <list type="bullet">
/// <item>Uses explicit <see cref="IDbTransaction"/> for atomic consistency with aggregate persistence</item>
/// <item>Uses <see cref="OutboxMessage"/> optimized for domain events</item>
/// <item>Does not include scheduling or statistics (simpler API)</item>
/// </list>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// using var transaction = connection.BeginTransaction();
///
/// // Save aggregate events
/// await eventStore.AppendAsync(aggregateId, events, transaction);
///
/// // Add outbox message in same transaction
/// await outbox.AddAsync(outboxMessage, transaction);
///
/// transaction.Commit();
/// </code>
/// </para>
/// <para>
/// <strong>Idempotency:</strong> Implementations must ensure that <see cref="GetPendingAsync"/>
/// and <see cref="MarkAsPublishedAsync"/> are idempotent using SQL-level guards
/// (e.g., WHERE PublishedAt IS NULL).
/// </para>
/// </remarks>
public interface IEventSourcedOutboxStore
{
	/// <summary>
	/// Adds a message to the outbox within the specified transaction.
	/// </summary>
	/// <param name="message">The outbox message to add. Must not be null.</param>
	/// <param name="transaction">The database transaction. Must not be null.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="transaction"/> is null.</exception>
	Task AddAsync(OutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken);

	/// <summary>
	/// Gets pending (unpublished) messages from the outbox in FIFO order (oldest first).
	/// </summary>
	/// <param name="batchSize">Maximum number of messages to retrieve. Default: 100.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of pending outbox messages, ordered by creation time (oldest first).</returns>
	Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as published.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to mark as published.</param>
	/// <param name="transaction">Optional database transaction. Pass null for standalone operation.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task MarkAsPublishedAsync(Guid messageId, IDbTransaction? transaction, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes published messages older than the specified retention period.
	/// </summary>
	/// <param name="retentionPeriod">The retention period for published messages.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of messages deleted.</returns>
	Task<int> DeletePublishedOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken);

	/// <summary>
	/// Increments the retry count for a message that failed to publish.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken);
}
