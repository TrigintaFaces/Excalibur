// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides persistent storage for outbound messages in the Transactional Outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// The outbox store ensures reliable message publishing by storing outbound messages within the same transaction as business data changes.
/// This guarantees:
/// </para>
/// <list type="bullet">
/// <item> Atomicity - messages are published only if business operations succeed </item>
/// <item> Reliability - messages are not lost due to transport failures </item>
/// <item> Consistency - outbound messages reflect actual state changes </item>
/// <item> Order preservation - messages are published in the correct sequence </item>
/// </list>
/// <para>
/// Messages are later published by a background service that polls the outbox.
/// </para>
/// <para>
/// This interface contains 5 core methods following the Microsoft IDistributedCache pattern.
/// For batch operations, implement <see cref="IOutboxStoreBatch"/>.
/// For admin/query operations, implement <see cref="IOutboxStoreAdmin"/>.
/// </para>
/// <para>
/// Interface uses ValueTask for synchronous completion optimization.
/// In-memory implementations complete synchronously without allocation overhead.
/// </para>
/// </remarks>
public interface IOutboxStore
{
	/// <summary>
	/// Stages a message in the outbox for later delivery.
	/// </summary>
	/// <param name="message"> The outbound message to stage. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stage operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when a message with the same ID already exists. </exception>
	ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Enqueues a message in the outbox for later delivery with context.
	/// </summary>
	/// <param name="message"> The message to enqueue. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous enqueue operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when message or context is null. </exception>
	ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves unsent messages from the outbox for publishing.
	/// </summary>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of unsent messages ready for delivery. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when batchSize is less than 1. </exception>
	ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as successfully sent.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message to mark as sent. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous mark-sent operation. </returns>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message does not exist or is already marked as sent. </exception>
	ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as failed during delivery.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message that failed. </param>
	/// <param name="errorMessage"> The error description or exception message. </param>
	/// <param name="retryCount"> The current retry attempt count. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous mark-failed operation. </returns>
	/// <exception cref="ArgumentException"> Thrown when messageId is null or empty. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when errorMessage is null. </exception>
	ValueTask MarkFailedAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a batch of messages as successfully sent in a single operation.
	/// </summary>
	/// <param name="messageIds"> The unique identifiers of the messages to mark as sent. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous batch mark-sent operation. </returns>
	/// <remarks>
	/// <para>
	/// The default implementation falls back to sequential per-message <see cref="MarkSentAsync"/> calls.
	/// Override in stores that support efficient batch updates (e.g., SQL <c>WHERE Id IN (...)</c>).
	/// </para>
	/// </remarks>
	async ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
	{
		foreach (var messageId in messageIds)
		{
			await MarkSentAsync(messageId, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Marks a batch of messages as failed in a single operation.
	/// </summary>
	/// <param name="messageIds"> The unique identifiers of the messages that failed. </param>
	/// <param name="reason"> The error description applied to all messages in the batch. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous batch mark-failed operation. </returns>
	/// <remarks>
	/// <para>
	/// The default implementation falls back to sequential per-message <see cref="MarkFailedAsync"/> calls.
	/// Override in stores that support efficient batch updates (e.g., SQL <c>WHERE Id IN (...)</c>).
	/// </para>
	/// </remarks>
	async ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, CancellationToken cancellationToken)
	{
		foreach (var messageId in messageIds)
		{
			await MarkFailedAsync(messageId, reason, 1, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Atomically marks message as sent in outbox and creates inbox entry for exactly-once delivery.
	/// </summary>
	/// <param name="messageId">The outbox message ID to mark as sent.</param>
	/// <param name="inboxEntry">The inbox entry to create for deduplication.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if transactional completion succeeded;
	/// <see langword="false"/> if not supported (store should fall back to MinimizedWindow).
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method enables exactly-once delivery semantics for
	/// <see cref="Options.Delivery.OutboxDeliveryGuarantee.TransactionalWhenApplicable"/>
	/// when outbox and inbox share the same database.
	/// </para>
	/// <para>
	/// The default implementation returns <see langword="false"/>. Override in stores that
	/// support atomic operations across outbox and inbox tables (e.g., SqlServerOutboxStore
	/// when configured with the same database as SqlServerInboxStore).
	/// </para>
	/// <para>
	/// <b>Atomicity guarantee:</b> Either both operations complete (message marked sent AND
	/// inbox entry created) or neither does. On failure, the transaction is rolled back.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="inboxEntry"/> is null.</exception>
	ValueTask<bool> TryMarkSentAndReceivedAsync(
		string messageId,
		InboxEntry inboxEntry,
		CancellationToken cancellationToken)
	{
		// Default implementation: transactional completion not supported
		return ValueTask.FromResult(false);
	}
}

/// <summary>
/// Provides administrative and query operations for outbox store management.
/// </summary>
/// <remarks>
/// <para>
/// These operations are used by background services, health checks, and administrative tooling.
/// They are NOT needed for normal outbox message flow (stage/send/fail).
/// Implementations should access this sub-interface via <c>GetService(typeof(IOutboxStoreAdmin))</c>
/// or direct DI registration.
/// </para>
/// </remarks>
public interface IOutboxStoreAdmin
{
	/// <summary>
	/// Retrieves failed messages that are eligible for retry.
	/// </summary>
	/// <param name="maxRetries"> Maximum number of retry attempts to consider. </param>
	/// <param name="olderThan"> Only return messages that failed before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of failed messages eligible for retry. </returns>
	ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves messages scheduled for future delivery.
	/// </summary>
	/// <param name="scheduledBefore"> Only return messages scheduled before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Collection of scheduled messages ready for delivery. </returns>
	ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Cleans up sent messages older than the specified age.
	/// </summary>
	/// <param name="olderThan"> Remove messages sent before this timestamp. </param>
	/// <param name="batchSize"> Maximum number of messages to remove in one operation. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The number of messages removed. </returns>
	ValueTask<int> CleanupSentMessagesAsync(
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the outbox store.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> Statistics including message counts by status and oldest unsent message age. </returns>
	ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
}
