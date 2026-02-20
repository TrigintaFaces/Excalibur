// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides persistent storage for incoming messages to ensure at-most-once processing semantics.
/// </summary>
/// <remarks>
/// <para>
/// The inbox store implements the Idempotent Consumer pattern by persistently tracking processed messages
/// before handler execution. Messages are keyed by a composite of <c>(messageId, handlerType)</c>, allowing
/// the same message to be processed independently by multiple handlers.
/// </para>
/// <para>This ensures:</para>
/// <list type="bullet">
/// <item><description>Duplicate detection - each handler processes a message at most once</description></item>
/// <item><description>Resilience - processing state survives application restarts</description></item>
/// <item><description>Consistency - messages are marked processed only after successful handling</description></item>
/// <item><description>Audit trail - complete processing history is maintained</description></item>
/// <item><description>Multi-handler support - different handlers can process the same message independently</description></item>
/// </list>
/// <para>
/// Interface uses ValueTask for synchronous completion optimization.
/// In-memory implementations complete synchronously without allocation overhead.
/// </para>
/// </remarks>
public interface IInboxStore
{
	/// <summary>
	/// Creates a new inbox entry for an incoming message and handler combination.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing this message.</param>
	/// <param name="messageType">The fully qualified type name of the message.</param>
	/// <param name="payload">The serialized message payload.</param>
	/// <param name="metadata">Additional message metadata including headers and context.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The created inbox entry with generated timestamps and initial status.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId, handlerType, or messageType is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when payload or metadata is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when an entry with the same (messageId, handlerType) already exists.</exception>
	ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as successfully processed for a specific handler.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to mark as processed.</param>
	/// <param name="handlerType">The fully qualified type name of the handler that processed the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous mark-processed operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the entry does not exist or is already processed.</exception>
	ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically attempts to mark a message as processed for a specific handler.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method provides atomic "first writer wins" semantics for idempotent message processing.
	/// If the message has not been processed by this handler, it creates the entry and returns <c>true</c>.
	/// If already processed, it returns <c>false</c> without throwing.
	/// </para>
	/// <para>
	/// This is the preferred method for idempotent message handling as it combines the check-and-mark
	/// operation atomically, preventing race conditions in concurrent processing scenarios.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <c>true</c> if this is the first time the handler is processing this message (entry created);
	/// <c>false</c> if the handler has already processed this message (duplicate).
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a message has already been processed by a specific handler.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to check.</param>
	/// <param name="handlerType">The fully qualified type name of the handler.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns><c>true</c> if the message has been processed by this handler; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves an inbox entry by message identifier and handler type.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to retrieve.</param>
	/// <param name="handlerType">The fully qualified type name of the handler.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The inbox entry if found; otherwise, <c>null</c>.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as failed during processing for a specific handler.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="handlerType">The fully qualified type name of the handler that failed.</param>
	/// <param name="errorMessage">The error description or exception message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous mark-failed operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when errorMessage is null.</exception>
	ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves failed message entries for retry processing.
	/// </summary>
	/// <param name="maxRetries">Maximum number of retry attempts to consider.</param>
	/// <param name="olderThan">Only return entries older than this timestamp.</param>
	/// <param name="batchSize">Maximum number of entries to return.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of failed inbox entries eligible for retry.</returns>
	ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all inbox entries for testing and diagnostics.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Collection of all inbox entries.</returns>
	ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about inbox entries.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>Statistics including counts of entries by status.</returns>
	ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Cleans up processed entries older than the specified retention period.
	/// </summary>
	/// <param name="retentionPeriod">Entries processed longer than this duration ago will be removed.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The number of entries removed.</returns>
	ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken);
}
