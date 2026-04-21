// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Defines message management operations for a long polling receiver.
/// </summary>
public interface ILongPollingReceiverMessageManagement
{
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
}
