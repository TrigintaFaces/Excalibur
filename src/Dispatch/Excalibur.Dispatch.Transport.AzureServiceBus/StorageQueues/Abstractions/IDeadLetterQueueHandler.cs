// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Handles dead letter queue operations for Azure Storage Queue messages.
/// </summary>
public interface IDeadLetterQueueHandler
{
	/// <summary>
	/// Determines whether a message should be sent to the dead letter queue.
	/// </summary>
	/// <param name="message"> The queue message. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="exception"> The exception that occurred during processing, if any. </param>
	/// <returns> True if the message should be dead lettered; otherwise, false. </returns>
	bool ShouldDeadLetter(QueueMessage message, IMessageContext context, Exception? exception = null);

	/// <summary>
	/// Sends a message to the dead letter queue.
	/// </summary>
	/// <param name="message"> The queue message to dead letter. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="reason"> The reason for dead lettering the message. </param>
	/// <param name="exception"> The exception that caused the message to be dead lettered, if any. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the dead letter operation. </returns>
	Task SendToDeadLetterAsync(QueueMessage message, IMessageContext context, string reason, CancellationToken cancellationToken, Exception? exception = null);

	/// <summary>
	/// Handles a poison message by either dead lettering it or applying other recovery strategies.
	/// </summary>
	/// <param name="message"> The poison message. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="exception"> The exception that caused the message to be considered poison. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the poison message handling operation. </returns>
	Task HandlePoisonMessageAsync(QueueMessage message, IMessageContext context, Exception exception,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the dead letter queue statistics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the dead letter queue statistics. </returns>
	Task<DeadLetterQueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Attempts to recover messages from the dead letter queue.
	/// </summary>
	/// <param name="maxMessages"> The maximum number of messages to recover. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the number of messages recovered. </returns>
	Task<int> RecoverMessagesAsync(CancellationToken cancellationToken, int maxMessages = 10);
}
