// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for a dead letter queue that stores failed messages for later inspection and replay.
/// </summary>
/// <remarks>
/// The dead letter queue is used to capture messages that cannot be processed after exhausting
/// all retry attempts. It provides capabilities for:
/// <list type="bullet">
///   <item>Storing failed messages with full exception context</item>
///   <item>Retrieving entries for inspection and debugging</item>
///   <item>Replaying messages for reprocessing</item>
///   <item>Purging old or resolved entries</item>
/// </list>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "DeadLetterQueue is a standard industry term in messaging systems")]
public interface IDeadLetterQueue
{
	/// <summary>
	/// Enqueues a message to the dead letter queue.
	/// </summary>
	/// <typeparam name="T">The type of message being dead lettered.</typeparam>
	/// <param name="message">The message that failed processing.</param>
	/// <param name="reason">The reason for dead lettering.</param>
	/// <param name="exception">Optional exception that caused the failure.</param>
	/// <param name="metadata">Optional additional metadata to store with the entry.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The ID of the created dead letter entry.</returns>
	Task<Guid> EnqueueAsync<T>(
		T message,
		DeadLetterReason reason,
		CancellationToken cancellationToken,
		Exception? exception = null,
		IDictionary<string, string>? metadata = null);

	/// <summary>
	/// Retrieves dead letter entries based on filter criteria.
	/// </summary>
	/// <param name="filter">Optional filter for querying entries. If null, returns all entries up to the limit.</param>
	/// <param name="limit">Maximum number of entries to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A read-only list of dead letter entries matching the criteria.</returns>
	Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
		CancellationToken cancellationToken,
		DeadLetterQueryFilter? filter = null,
		int limit = 100);

	/// <summary>
	/// Retrieves a specific dead letter entry by its ID.
	/// </summary>
	/// <param name="entryId">The unique identifier of the entry.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The dead letter entry if found, null otherwise.</returns>
	Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>
	/// Replays a dead letter entry, re-submitting it for processing.
	/// </summary>
	/// <param name="entryId">The unique identifier of the entry to replay.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the entry was successfully replayed, false if not found.</returns>
	Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>
	/// Replays multiple dead letter entries that match the specified filter.
	/// </summary>
	/// <param name="filter">Filter criteria for selecting entries to replay.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of entries successfully replayed.</returns>
	Task<int> ReplayBatchAsync(DeadLetterQueryFilter filter, CancellationToken cancellationToken);

	/// <summary>
	/// Purges (permanently deletes) a dead letter entry.
	/// </summary>
	/// <param name="entryId">The unique identifier of the entry to purge.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the entry was successfully purged, false if not found.</returns>
	Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken);

	/// <summary>
	/// Purges all dead letter entries older than the specified age.
	/// </summary>
	/// <param name="olderThan">The age threshold for purging entries.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of entries purged.</returns>
	Task<int> PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current count of entries in the dead letter queue.
	/// </summary>
	/// <param name="filter">Optional filter to count specific entries.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of entries matching the filter criteria.</returns>
	Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null);
}
