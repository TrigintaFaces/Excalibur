// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the contract for batch message receiving from Google Pub/Sub.
/// </summary>
public interface IBatchReceiver
{
	/// <summary>
	/// Receives a batch of messages from the subscription.
	/// </summary>
	/// <param name="subscriptionName"> The subscription to receive messages from. </param>
	/// <param name="maxMessages"> Maximum number of messages to receive in the batch. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A batch of received messages. </returns>
	Task<MessageBatch> ReceiveBatchAsync(
		SubscriptionName subscriptionName,
		int maxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Receives multiple batches of messages with adaptive sizing.
	/// </summary>
	/// <param name="subscriptionName"> The subscription to receive messages from. </param>
	/// <param name="batchCount"> Number of batches to receive. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> An async enumerable of message batches. </returns>
	IAsyncEnumerable<MessageBatch> ReceiveBatchesAsync(
		SubscriptionName subscriptionName,
		int batchCount,
		CancellationToken cancellationToken);
}
