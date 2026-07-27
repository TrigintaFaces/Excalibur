// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines the receive-side contract for a message channel.
/// </summary>
/// <typeparam name="TMessage"> The type of message handled by the channel. </typeparam>
public interface IMessageChannelReceiver<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Receives a message from the channel.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns>
	/// A task that represents the asynchronous receive operation. The task result contains the received message, or null if no message is available.
	/// </returns>
	Task<TMessage?> ReceiveAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Receives a batch of messages from the channel.
	/// </summary>
	/// <param name="maxMessages"> The maximum number of messages to receive. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous batch receive operation. The task result contains the received messages. </returns>
	Task<IEnumerable<TMessage>>
		ReceiveBatchAsync(int maxMessages, CancellationToken cancellationToken);
}
