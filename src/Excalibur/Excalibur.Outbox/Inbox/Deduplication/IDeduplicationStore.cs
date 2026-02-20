// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines a store for message deduplication tracking.
/// </summary>
public interface IDeduplicationStore
{
	/// <summary>
	/// Checks if a message has been processed.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the message has been processed; otherwise, false. </returns>
	Task<bool> ContainsAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as processed.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="expiry"> Optional expiry time for the deduplication entry. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task AddAsync(string messageId, TimeSpan? expiry, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a message from the deduplication store.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the message was removed; otherwise, false. </returns>
	Task<bool> RemoveAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Clears expired entries from the store.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The number of entries removed. </returns>
	Task<int> ClearExpiredAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a message has been processed and marks it as processed if not.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="context"> The deduplication context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A result indicating whether the message is a duplicate. </returns>
	Task<DeduplicationResult> CheckAndMarkAsync(string messageId, DeduplicationContext context,
		CancellationToken cancellationToken);
}
