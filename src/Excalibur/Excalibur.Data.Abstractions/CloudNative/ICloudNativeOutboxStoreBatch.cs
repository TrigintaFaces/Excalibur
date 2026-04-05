// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Provides batch and administrative operations for cloud-native outbox stores.
/// Implementations should implement this alongside <see cref="ICloudNativeOutboxStore"/>.
/// </summary>
public interface ICloudNativeOutboxStoreBatch
{
	/// <summary>Adds multiple messages in a transactional batch.</summary>
	Task<CloudBatchResult> AddBatchAsync(IEnumerable<CloudOutboxMessage> messages, IPartitionKey partitionKey, CancellationToken cancellationToken);

	/// <summary>Marks multiple messages as published in a batch.</summary>
	Task<CloudBatchResult> MarkBatchAsPublishedAsync(IEnumerable<string> messageIds, IPartitionKey partitionKey, CancellationToken cancellationToken);

	/// <summary>Deletes published messages older than the retention period.</summary>
	Task<CloudCleanupResult> CleanupOldMessagesAsync(IPartitionKey partitionKey, TimeSpan retentionPeriod, CancellationToken cancellationToken);

	/// <summary>Increments the retry count for a failed message.</summary>
	Task<CloudOperationResult> IncrementRetryCountAsync(string messageId, IPartitionKey partitionKey, string? errorMessage, CancellationToken cancellationToken);
}
