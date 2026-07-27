// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Provides batch and transactional operations for outbox stores.
/// Implementations that support efficient batch operations should implement this interface
/// alongside <see cref="IOutboxStore"/>.
/// </summary>
public interface IOutboxStoreBatch
{
	/// <summary>Marks a batch of messages as successfully sent.</summary>
	ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken);

	/// <summary>Marks a batch of messages as failed.</summary>
	ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken);

	/// <summary>Atomically marks message as sent and creates the inbox entry in one transaction, so a redelivery is skipped rather than reprocessed (effectively-once processing; delivery remains at-least-once).</summary>
	ValueTask<bool> TryMarkSentAndReceivedAsync(string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken);
}
