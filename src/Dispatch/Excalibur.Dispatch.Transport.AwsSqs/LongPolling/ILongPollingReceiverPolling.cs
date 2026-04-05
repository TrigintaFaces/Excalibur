// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Defines polling operations for receiving AWS SQS messages.
/// </summary>
public interface ILongPollingReceiverPolling
{
	/// <summary>
	/// Receives messages from the specified queue.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue to receive messages from. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask{T}"/> representing the asynchronous operation, containing the received messages.</returns>
	ValueTask<IReadOnlyList<Message>> ReceiveMessagesAsync(
		string queueUrl,
		CancellationToken cancellationToken);

	/// <summary>
	/// Receives messages from the specified queue with options.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue to receive messages from. </param>
	/// <param name="options"> The receive options. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask{T}"/> representing the asynchronous operation, containing the received messages.</returns>
	ValueTask<IReadOnlyList<Message>> ReceiveMessagesAsync(
		string queueUrl,
		ReceiveOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Starts continuous polling for messages from the specified queue.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue to poll. </param>
	/// <param name="messageHandler"> The handler for processing individual messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask StartContinuousPollingAsync(
		string queueUrl,
		Func<Message, CancellationToken, ValueTask> messageHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Starts continuous polling for messages from the specified queue with batch processing.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue to poll. </param>
	/// <param name="batchHandler"> The handler for processing batches of messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask StartContinuousPollingAsync(
		string queueUrl,
		Func<IReadOnlyList<Message>, CancellationToken, ValueTask> batchHandler,
		CancellationToken cancellationToken);
}
