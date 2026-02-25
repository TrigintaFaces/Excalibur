// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides bulk cleanup operations for outbox stores (ISP sub-interface of <see cref="IOutboxStoreAdmin"/>).
/// </summary>
/// <remarks>
/// <para>
/// This interface separates bulk cleanup concerns from the core <see cref="IOutboxStoreAdmin"/> interface,
/// following the Interface Segregation Principle. Implementations can perform efficient batch deletions
/// with configurable batch sizes to avoid locking the database for extended periods.
/// </para>
/// <para>
/// Access via DI: <c>services.GetService&lt;IOutboxBulkCleanup&gt;()</c>
/// </para>
/// </remarks>
public interface IOutboxBulkCleanup
{
	/// <summary>
	/// Deletes sent messages in batches, processing up to <paramref name="batchSize"/> per iteration.
	/// </summary>
	/// <param name="olderThan">Remove messages sent before this timestamp.</param>
	/// <param name="batchSize">Maximum number of messages to delete per batch iteration.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The total number of messages deleted across all batches.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
	ValueTask<int> BulkCleanupSentMessagesAsync(
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes failed messages that have exceeded the maximum retry count in batches.
	/// </summary>
	/// <param name="maxRetries">Only delete messages that have exceeded this retry count.</param>
	/// <param name="olderThan">Only delete messages that failed before this timestamp.</param>
	/// <param name="batchSize">Maximum number of messages to delete per batch iteration.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The total number of messages deleted across all batches.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
	ValueTask<int> BulkCleanupFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset olderThan,
		int batchSize,
		CancellationToken cancellationToken);
}
