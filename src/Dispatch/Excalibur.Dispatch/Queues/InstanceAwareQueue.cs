// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Queues;

// R0.8: Identifiers should not have incorrect suffix - Queue is appropriate for this type
#pragma warning disable CA1711

/// <summary>
/// Provides an instance-aware wrapper around a distributed ordered set queue that tracks and manages items added by a specific instance.
/// This enables cleanup and isolation of queue items by instance identifier.
/// </summary>
/// <param name="instanceId"> The unique identifier for this instance that will be prefixed to all values. </param>
/// <param name="queue"> The underlying distributed queue implementation to wrap. </param>
public sealed class InstanceAwareQueue(string instanceId, IDistributedOrderedSetQueue<string> queue) : IAsyncDisposable
#pragma warning restore CA1711
{
	private readonly Channel<string> _ownedItems = Channel.CreateUnbounded<string>();

	/// <summary>
	/// Adds a value to the queue prefixed with the instance identifier. The value is tracked in the owned items collection for later cleanup.
	/// </summary>
	/// <param name="value"> The value to add to the queue. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns true if the item was added successfully, false otherwise. </returns>
	public async Task<bool> AddAsync(string value, CancellationToken cancellationToken)
	{
		var instanceScopedValue = $"{instanceId}:{value}";
		if (await queue.AddAsync(instanceScopedValue, cancellationToken).ConfigureAwait(false))
		{
			_ = _ownedItems.Writer.TryWrite(instanceScopedValue);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Removes a specific owned item from the distributed queue. The item must be a fully qualified instance-scoped value.
	/// </summary>
	/// <param name="ownedItem"> The instance-scoped item to remove from the queue. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns true if the item was removed successfully, false otherwise. </returns>
	public async Task<bool> RemoveAsync(string ownedItem, CancellationToken cancellationToken) =>
		await queue.RemoveAsync(ownedItem, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Attempts to pop an item from the queue and strips the instance prefix to return the original value. Items from any instance may be
	/// returned, but the instance prefix is removed.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that returns a tuple containing success status and the value without instance prefix. </returns>
	// MA0038: Cannot make method static - requires access to queue instance parameter from primary constructor
#pragma warning disable MA0038

	public async Task<(bool Success, string? Value)> TryPopAsync(CancellationToken cancellationToken)
	{
		var (success, item) = await queue.TryPopAsync(cancellationToken).ConfigureAwait(false);
		if (!success || item == null)
		{
			return (false, null);
		}

		var parts = item.Split(':', 2);
		return (true, parts.Length == 2 ? parts[1] : item);
	}

#pragma warning restore MA0038

	/// <summary>
	/// Removes all items that were added by this instance from the distributed queue. This method processes all items in the owned items
	/// collection and attempts to remove them.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that completes when all owned items have been processed for removal. </returns>
	public async Task CleanupAsync(CancellationToken cancellationToken)
	{
		while (_ownedItems.Reader.TryRead(out var ownedItem))
		{
			// Remove only those inserted by this instance
			_ = await RemoveAsync(ownedItem, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously disposes the queue by cleaning up all owned items and suppressing finalization. This ensures that all items added by
	/// this instance are removed from the distributed queue.
	/// </summary>
	/// <returns> A task that completes when the disposal is finished. </returns>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync(CancellationToken.None).ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}
}
