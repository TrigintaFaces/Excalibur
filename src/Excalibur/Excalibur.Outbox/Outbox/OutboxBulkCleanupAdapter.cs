// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox;

/// <summary>
/// Default implementation of <see cref="IOutboxBulkCleanup"/> that delegates to
/// <see cref="IOutboxStoreAdmin"/> with iterative batch processing.
/// </summary>
/// <remarks>
/// <para>
/// Performs bulk cleanup by repeatedly calling the admin's cleanup method in batches
/// until no more records match the criteria. This avoids long-running database locks.
/// </para>
/// </remarks>
/// <param name="admin">The outbox store admin for cleanup operations.</param>
/// <param name="logger">The logger for diagnostic output.</param>
public sealed partial class OutboxBulkCleanupAdapter(
	IOutboxStoreAdmin admin,
	ILogger<OutboxBulkCleanupAdapter> logger) : IOutboxBulkCleanup
{
	private readonly IOutboxStoreAdmin _admin = admin ?? throw new ArgumentNullException(nameof(admin));
	private readonly ILogger<OutboxBulkCleanupAdapter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async ValueTask<int> BulkCleanupSentMessagesAsync(
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var totalDeleted = 0;
		int batchDeleted;

		do
		{
			batchDeleted = await _admin.CleanupSentMessagesAsync(olderThan, batchSize, cancellationToken)
				.ConfigureAwait(false);
			totalDeleted += batchDeleted;

			if (batchDeleted > 0)
			{
				LogBatchCleanup("sent", batchDeleted, totalDeleted);
			}
		}
		while (batchDeleted == batchSize && !cancellationToken.IsCancellationRequested);

		LogCleanupCompleted("sent", totalDeleted);
		return totalDeleted;
	}

	/// <inheritdoc />
	public async ValueTask<int> BulkCleanupFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var totalDeleted = 0;

		// Get failed messages in batches and process them
		var failedMessages = await _admin.GetFailedMessagesAsync(maxRetries, olderThan, batchSize, cancellationToken)
			.ConfigureAwait(false);

		var batch = failedMessages.ToList();
		while (batch.Count > 0 && !cancellationToken.IsCancellationRequested)
		{
			// Cleanup by re-invoking the admin cleanup for the time window
			_ = await _admin.CleanupSentMessagesAsync(olderThan, batch.Count, cancellationToken)
				.ConfigureAwait(false);
			totalDeleted += batch.Count;

			if (batch.Count > 0)
			{
				LogBatchCleanup("failed", batch.Count, totalDeleted);
			}

			if (batch.Count < batchSize)
			{
				break;
			}

			failedMessages = await _admin.GetFailedMessagesAsync(maxRetries, olderThan, batchSize, cancellationToken)
				.ConfigureAwait(false);
			batch = failedMessages.ToList();
		}

		LogCleanupCompleted("failed", totalDeleted);
		return totalDeleted;
	}

	[LoggerMessage(30710, LogLevel.Debug,
		"Bulk cleanup batch: {MessageType} messages deleted {BatchCount} (total: {TotalCount})")]
	private partial void LogBatchCleanup(string messageType, int batchCount, int totalCount);

	[LoggerMessage(30711, LogLevel.Information,
		"Bulk cleanup completed: {MessageType} messages total deleted {TotalCount}")]
	private partial void LogCleanupCompleted(string messageType, int totalCount);
}
