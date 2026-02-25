// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.SQS.Model;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Defines a long polling receiver for AWS SQS messages.
/// </summary>
public interface ILongPollingReceiver : IDisposable
{
	/// <summary>
	/// Gets the current polling status.
	/// </summary>
	PollingStatus Status { get; }

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

	/// <summary>
	/// Optimizes the visibility timeout for a message based on estimated processing time.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue. </param>
	/// <param name="receiptHandle"> The receipt handle of the message. </param>
	/// <param name="estimatedProcessingTime"> The estimated time required to process the message. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask OptimizeVisibilityTimeoutAsync(
		string queueUrl,
		string receiptHandle,
		TimeSpan estimatedProcessingTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a message from the queue.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue. </param>
	/// <param name="receiptHandle"> The receipt handle of the message to delete. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask DeleteMessageAsync(
		string queueUrl,
		string receiptHandle,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes multiple messages from the queue.
	/// </summary>
	/// <param name="queueUrl"> The URL of the queue. </param>
	/// <param name="receiptHandles"> The receipt handles of the messages to delete. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask DeleteMessagesAsync(
		string queueUrl,
		IEnumerable<string> receiptHandles,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the receiver's operations.
	/// </summary>
	/// <returns>A <see cref="ValueTask{T}"/> representing the asynchronous operation, containing the receiver statistics.</returns>
	ValueTask<ReceiverStatistics> GetStatisticsAsync();

	/// <summary>
	/// Starts the receiver.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the receiver.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
