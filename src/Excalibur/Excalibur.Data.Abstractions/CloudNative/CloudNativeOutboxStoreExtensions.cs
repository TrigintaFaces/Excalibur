// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Extension methods for <see cref="ICloudNativeOutboxStore"/>.
/// </summary>
public static class CloudNativeOutboxStoreExtensions
{
	/// <summary>Adds multiple messages in a transactional batch.</summary>
	public static Task<CloudBatchResult> AddBatchAsync(this ICloudNativeOutboxStore store, IEnumerable<CloudOutboxMessage> messages, IPartitionKey partitionKey, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is ICloudNativeOutboxStoreBatch batch)
		{
			return batch.AddBatchAsync(messages, partitionKey, cancellationToken);
		}

		return Task.FromResult(new CloudBatchResult(false, 0, []));
	}

	/// <summary>Marks multiple messages as published in a batch.</summary>
	public static Task<CloudBatchResult> MarkBatchAsPublishedAsync(this ICloudNativeOutboxStore store, IEnumerable<string> messageIds, IPartitionKey partitionKey, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is ICloudNativeOutboxStoreBatch batch)
		{
			return batch.MarkBatchAsPublishedAsync(messageIds, partitionKey, cancellationToken);
		}

		return Task.FromResult(new CloudBatchResult(false, 0, []));
	}

	/// <summary>Deletes published messages older than the retention period.</summary>
	public static Task<CloudCleanupResult> CleanupOldMessagesAsync(this ICloudNativeOutboxStore store, IPartitionKey partitionKey, TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is ICloudNativeOutboxStoreBatch batch)
		{
			return batch.CleanupOldMessagesAsync(partitionKey, retentionPeriod, cancellationToken);
		}

		return Task.FromResult(new CloudCleanupResult(0, 0));
	}

	/// <summary>Increments the retry count for a failed message.</summary>
	public static Task<CloudOperationResult> IncrementRetryCountAsync(this ICloudNativeOutboxStore store, string messageId, IPartitionKey partitionKey, string? errorMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is ICloudNativeOutboxStoreBatch batch)
		{
			return batch.IncrementRetryCountAsync(messageId, partitionKey, errorMessage, cancellationToken);
		}

		return Task.FromResult(new CloudOperationResult(false, 0, 0));
	}
}
