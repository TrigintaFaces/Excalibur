// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Persists subscription checkpoint positions for durable subscriptions.
/// </summary>
/// <remarks>
/// <para>
/// Checkpoint stores enable event subscriptions to resume from their last known
/// position after a restart. Each subscription is identified by a unique name.
/// </para>
/// <para>
/// Follows the pattern from <c>Azure.Messaging.ServiceBus.ServiceBusProcessor</c>
/// which uses checkpoint-based position tracking for message processing.
/// </para>
/// </remarks>
public interface ISubscriptionCheckpointStore
{
	/// <summary>
	/// Gets the last checkpointed position for a named subscription.
	/// </summary>
	/// <param name="subscriptionName">The unique subscription identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The last stored position, or <see langword="null"/> if no checkpoint exists
	/// (indicating the subscription should start from the beginning).
	/// </returns>
	Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken);

	/// <summary>
	/// Stores the checkpoint position for a named subscription.
	/// </summary>
	/// <param name="subscriptionName">The unique subscription identifier.</param>
	/// <param name="position">The position to checkpoint.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task StoreCheckpointAsync(string subscriptionName, long position, CancellationToken cancellationToken);
}
