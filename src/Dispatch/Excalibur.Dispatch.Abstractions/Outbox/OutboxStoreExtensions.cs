// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IOutboxStore"/>.
/// </summary>
public static class OutboxStoreExtensions
{
	/// <summary>Marks a batch of messages as successfully sent.</summary>
	public static async ValueTask MarkBatchSentAsync(this IOutboxStore store, IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchSentAsync(messageIds, cancellationToken).ConfigureAwait(false);
			return;
		}

		foreach (var messageId in messageIds)
		{
			await store.MarkSentAsync(messageId, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Marks a batch of messages as failed.</summary>
	public static async ValueTask MarkBatchFailedAsync(this IOutboxStore store, IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is IOutboxStoreBatch batch)
		{
			await batch.MarkBatchFailedAsync(messageIds, reason, retryCount, cancellationToken).ConfigureAwait(false);
			return;
		}

		foreach (var messageId in messageIds)
		{
			await store.MarkFailedAsync(messageId, reason, retryCount, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Atomically marks message as sent and creates inbox entry for exactly-once delivery.</summary>
	public static ValueTask<bool> TryMarkSentAndReceivedAsync(this IOutboxStore store, string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is IOutboxStoreBatch batch)
		{
			return batch.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken);
		}
		return ValueTask.FromResult(false);
	}
}
