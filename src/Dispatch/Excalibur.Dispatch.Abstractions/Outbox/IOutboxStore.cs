// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides persistent storage for outbound messages in the Transactional Outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Traditional (polling) outbox</strong> for relational and document databases
/// (SQL Server, Postgres, MongoDB, etc.). Messages are staged within a database
/// transaction alongside business data, then published by a background polling service
/// (<c>OutboxBackgroundService</c>).
/// </para>
/// <para>
/// For cloud-native databases that use change-feed triggers instead of polling
/// (Cosmos DB, DynamoDB, Firestore), see
/// <c>Excalibur.Data.Abstractions.CloudNative.ICloudNativeOutboxStore</c>.
/// The two interfaces serve fundamentally different outbox patterns and are
/// intentionally separate:
/// </para>
/// <list type="bullet">
/// <item><c>IOutboxStore</c> -- polling-based, SQL transactions, background service</item>
/// <item><c>ICloudNativeOutboxStore</c> -- change-feed triggers, partition keys, serverless</item>
/// </list>
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
