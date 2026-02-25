// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for lightweight in-memory message deduplication without full inbox persistence.
/// </summary>
/// <remarks>
/// <para>
/// This provides a simpler alternative to full inbox persistence for scenarios where:
/// </para>
/// <list type="bullet">
/// <item><description>Message volume is moderate</description></item>
/// <item><description>Duplicate detection window is short (hours, not days)</description></item>
/// <item><description>Message loss on restart is acceptable</description></item>
/// <item><description>Performance is prioritized over durability</description></item>
/// </list>
/// </remarks>
public interface IInMemoryDeduplicator
{
	/// <summary>
	/// Checks if a message ID has already been processed within the expiry window.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="expiry"> How long to remember the message ID. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> <see langword="true"/> if the message is a duplicate; otherwise, <see langword="false"/>. </returns>
	Task<bool> IsDuplicateAsync(string messageId, TimeSpan expiry, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as processed to prevent duplicate processing.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="expiry"> How long to remember the message ID. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A <see cref="Task"/> representing the asynchronous operation. </returns>
	Task MarkProcessedAsync(string messageId, TimeSpan expiry, CancellationToken cancellationToken);

	/// <summary>
	/// Removes expired entries from the deduplicator.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The number of entries removed. </returns>
	Task<int> CleanupExpiredEntriesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets current statistics about deduplicator performance and usage.
	/// </summary>
	/// <returns> Statistics about the deduplicator. </returns>
	DeduplicationStatistics GetStatistics();

	/// <summary>
	/// Clears all tracked message IDs from memory.
	/// </summary>
	/// <returns> A <see cref="Task"/> representing the asynchronous operation. </returns>
	Task ClearAsync();
}
