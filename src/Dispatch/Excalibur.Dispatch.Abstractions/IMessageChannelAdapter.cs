// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a contract for adapting message channels to a common interface.
/// </summary>
/// <typeparam name="TMessage"> The type of message handled by the adapter. </typeparam>
public interface IMessageChannelAdapter<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Gets the channel name or identifier.
	/// </summary>
	/// <value> The logical identifier of the channel. </value>
	string ChannelName { get; }

	/// <summary>
	/// Gets a value indicating whether the adapter is currently connected.
	/// </summary>
	/// <value> <see langword="true" /> when the adapter maintains an active connection; otherwise, <see langword="false" />. </value>
	bool IsConnected { get; }

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

	/// <summary>
	/// Acknowledges successful processing of a message.
	/// </summary>
	/// <param name="message"> The message to acknowledge. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous acknowledge operation. </returns>
	Task AcknowledgeAsync(TMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Rejects a message, potentially moving it to a dead letter queue.
	/// </summary>
	/// <param name="message"> The message to reject. </param>
	/// <param name="reason"> The reason for rejection. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous reject operation. </returns>
	Task RejectAsync(TMessage message, string reason, CancellationToken cancellationToken);

	/// <summary>
	/// Connects to the channel.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous connect operation. </returns>
	Task ConnectAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Disconnects from the channel.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous disconnect operation. </returns>
	Task DisconnectAsync(CancellationToken cancellationToken);
}
