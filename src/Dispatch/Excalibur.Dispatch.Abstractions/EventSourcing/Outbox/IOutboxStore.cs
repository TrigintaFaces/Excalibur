// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Transactional outbox store for reliable message publishing.
/// Provides low-level CRUD operations for outbox entries.
///
/// <remarks>
/// <para>
/// The outbox pattern ensures that domain events are published reliably by storing them
/// in the same database transaction as the aggregate changes. A background service later
/// reads pending messages and publishes them to the message bus.
/// </para>
///
/// <para>
/// This interface supports explicit transaction control via <see cref="IDbTransaction"/>,
/// allowing atomic consistency between aggregate persistence and outbox message storage.
/// </para>
///
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
///
/// <para>
/// <strong>Idempotency:</strong> Implementations must ensure that <see cref="GetPendingAsync"/>
/// and <see cref="MarkAsPublishedAsync"/> are idempotent using SQL-level guards
/// (e.g., WHERE PublishedAt IS NULL).
/// </para>
///
/// <para>
/// <strong>Storage Strategy:</strong> Full payload (JSON-serialized domain event) is stored
/// for reliable publishing and audit trail.
/// </para>
///
/// <para>
/// <strong>Cleanup Strategy:</strong> Published messages are retained for audit purposes
/// with configurable retention period (default: 7 days). Use <see cref="DeletePublishedOlderThanAsync"/>
/// for cleanup.
/// </para>
/// </remarks>
/// </summary>
public interface IEventOutboxStore
{
	/// <summary>
	/// Adds a message to the outbox within the specified transaction.
	///
	/// <para>
	/// This method MUST be called within a database transaction to ensure atomic consistency
	/// between aggregate changes and outbox message storage.
	/// </para>
	///
	/// <para>
	/// <strong>Thread Safety:</strong> This method is thread-safe when used with proper transaction isolation.
	/// </para>
	/// </summary>
	/// <param name="message">The outbox message to add. Must not be null.</param>
	/// <param name="transaction">The database transaction. Must not be null.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="transaction"/> is null.</exception>
	Task AddAsync(OutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken);

	/// <summary>
	/// Gets pending (unpublished) messages from the outbox in FIFO order (oldest first).
	///
	/// <para>
	/// Messages are ordered by <see cref="OutboxMessage.CreatedAt"/> ASC to ensure
	/// first-in-first-out processing order.
	/// </para>
	///
	/// <para>
	/// <strong>Idempotency:</strong> Only returns messages where PublishedAt IS NULL.
	/// </para>
	///
	/// <para>
	/// <strong>Performance:</strong> Uses indexed query on PublishedAt for fast retrieval.
	/// </para>
	/// </summary>
	/// <param name="batchSize">Maximum number of messages to retrieve. Default: 100.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of pending outbox messages, ordered by creation time (oldest first).</returns>
	Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as published.
	///
	/// <para>
	/// <strong>Idempotency:</strong> Safe to call multiple times for the same message.
	/// Uses SQL-level guard (WHERE PublishedAt IS NULL) to prevent duplicate marking.
	/// </para>
	///
	/// <para>
	/// <strong>Transaction Support:</strong> The <paramref name="transaction"/> parameter is optional.
	/// Pass null for standalone publishing, or provide a transaction for atomic operations.
	/// </para>
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to mark as published.</param>
	/// <param name="transaction">Optional database transaction. Pass null for standalone operation.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task MarkAsPublishedAsync(Guid messageId, IDbTransaction? transaction, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes published messages older than the specified retention period.
	///
	/// <para>
	/// <strong>Cleanup Strategy:</strong> Published messages are retained for audit purposes.
	/// This method removes only messages where PublishedAt IS NOT NULL AND PublishedAt &lt; cutoffDate.
	/// </para>
	///
	/// <para>
	/// <strong>Default Retention:</strong> 7 days (configurable by caller).
	/// </para>
	///
	/// <para>
	/// <strong>Performance:</strong> Uses indexed query on PublishedAt for efficient deletion.
	/// </para>
	/// </summary>
	/// <param name="retentionPeriod">The retention period for published messages. Messages older than this will be deleted.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of messages deleted.</returns>
	Task<int> DeletePublishedOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken);

	/// <summary>
	/// Increments the retry count for a message that failed to publish.
	///
	/// <para>
	/// <strong>Usage:</strong> Called by the outbox publisher when a message fails to publish.
	/// The retry count can be used to implement exponential backoff or dead-letter queue logic.
	/// </para>
	///
	/// <para>
	/// <strong>Idempotency:</strong> Safe to call multiple times. Each call increments the count by 1.
	/// </para>
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken);
}
