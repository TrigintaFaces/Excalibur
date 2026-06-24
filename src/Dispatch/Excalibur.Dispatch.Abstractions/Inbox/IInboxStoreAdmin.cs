// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Provides administrative and query operations for inbox store management.
/// </summary>
/// <remarks>
/// <para>
/// These operations are used by background services, health checks, retry processors,
/// and administrative tooling. They are NOT needed for normal inbox message flow
/// (create, check, mark processed/failed).
/// </para>
/// <para>
/// This follows the same ISP pattern as <see cref="IOutboxStoreAdmin"/> for the base
/// <see cref="IOutboxStore"/>. Implementations should register this sub-interface
/// separately in DI so consumers can resolve it independently.
/// </para>
/// <para>
/// <strong>Interface split:</strong>
/// <list type="bullet">
/// <item><see cref="IInboxStore"/> -- Core: create, check, mark processed/failed, get entry (6 methods, hot path)</item>
/// <item><see cref="IInboxStoreAdmin"/> -- Admin: bulk queries, statistics, cleanup, retry-processor mark-failed (5 methods, operational)</item>
/// </list>
/// </para>
/// </remarks>
public interface IInboxStoreAdmin
{
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
	/// Marks an existing inbox entry as failed/retryable, setting its retry count <strong>exactly</strong> to
	/// <paramref name="retryCount"/> without auto-incrementing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a retry-processor operation for the transient-failure case: a short-circuit (e.g. an open
	/// circuit breaker) is not a delivery attempt, so it must leave the message re-admittable for retry
	/// <em>without</em> consuming an attempt. Unlike the core
	/// <see cref="IInboxStore.MarkFailedAsync(string, string, string, CancellationToken)"/> (which increments
	/// the retry count), this overload sets the count exactly — symmetric with
	/// <see cref="IOutboxStore.MarkFailedAsync(string, string, int, CancellationToken)"/>.
	/// </para>
	/// <para>
	/// The entry must already exist; existence semantics match the core method (implementations that update
	/// in place leave a missing entry unchanged or throw, consistent with their existing behavior). This is a
	/// retry-only path that processes already-persisted failed entries, so no insert/upsert is performed.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="handlerType">The handler type the entry is keyed to.</param>
	/// <param name="errorMessage">The error description to record.</param>
	/// <param name="retryCount">The retry count to set on the entry, exactly (not incremented).</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous mark-failed operation.</returns>
	ValueTask MarkFailedAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
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
	/// Cleans up processed entries older than the specified timestamp.
	/// </summary>
	/// <param name="olderThan">Remove entries processed before this timestamp.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The number of entries removed.</returns>
	ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
