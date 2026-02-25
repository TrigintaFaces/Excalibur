// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Queues;

// R0.8: Identifiers should not have incorrect suffix - Queue is appropriate for this type
#pragma warning disable CA1711

/// <summary>
/// Defines a distributed ordered set queue that maintains unique items in insertion order across multiple instances. Unlike regular queues,
/// this queue ensures that duplicate items are not added and supports random access removal.
/// </summary>
/// <typeparam name="T"> The type of items stored in the queue. </typeparam>
public interface IDistributedOrderedSetQueue<T>
#pragma warning restore CA1711
{
	/// <summary>
	/// Adds an item to the queue if it doesn't already exist. The item is appended to the end of the queue maintaining insertion order.
	/// </summary>
	/// <param name="item"> The item to add to the queue. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns true if the item was added, false if it already exists. </returns>
	Task<bool> AddAsync(T item, CancellationToken cancellationToken);

	/// <summary>
	/// Attempts to remove and return the first item from the queue. This operation maintains FIFO ordering for the set queue.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns a tuple with success status and the popped item if successful. </returns>
	Task<(bool Success, T? Item)> TryPopAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether the specified item exists in the queue. This operation supports set semantics by allowing efficient membership testing.
	/// </summary>
	/// <param name="item"> The item to search for in the queue. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns true if the item exists in the queue, false otherwise. </returns>
	Task<bool> ContainsAsync(T item, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current number of unique items in the queue. This count reflects the set nature of the queue where duplicates are not allowed.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns the number of items currently in the queue. </returns>
	Task<int> CountAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Removes a specific item from the queue regardless of its position. This operation supports set semantics by allowing random access removal.
	/// </summary>
	/// <param name="item"> The specific item to remove from the queue. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns true if the item was found and removed, false if not found. </returns>
	Task<bool> RemoveAsync(T item, CancellationToken cancellationToken);
}
