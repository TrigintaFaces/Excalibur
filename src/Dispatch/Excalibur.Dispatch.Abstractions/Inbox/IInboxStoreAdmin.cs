// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

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
/// <item><see cref="IInboxStoreAdmin"/> -- Admin: bulk queries, statistics, cleanup (4 methods, operational)</item>
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
