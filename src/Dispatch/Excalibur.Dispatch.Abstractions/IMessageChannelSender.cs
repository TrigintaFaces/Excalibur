// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines the send-side contract for a message channel.
/// </summary>
/// <typeparam name="TMessage"> The type of message handled by the channel. </typeparam>
public interface IMessageChannelSender<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Sends a message through the channel.
	/// </summary>
	/// <param name="message"> The message to send. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous send operation. </returns>
	Task SendAsync(TMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a batch of messages through the channel.
	/// </summary>
	/// <param name="messages"> The messages to send. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous batch send operation. </returns>
	Task SendBatchAsync(IEnumerable<TMessage> messages, CancellationToken cancellationToken);
}
