// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Manages dead letter queue operations for failed messages.
/// </summary>
public interface IDeadLetterQueueManager
{
	/// <summary>
	/// Moves a message to the dead letter queue.
	/// </summary>
	/// <param name="message"> The message to move to DLQ. </param>
	/// <param name="reason"> The reason for dead lettering. </param>
	/// <param name="exception"> Optional exception that caused the failure. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The ID of the message in the DLQ. </returns>
	Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves messages from the dead letter queue.
	/// </summary>
	/// <param name="maxMessages"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A list of dead letter messages. </returns>
	Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reprocesses messages from the dead letter queue.
	/// </summary>
	/// <param name="messages"> Messages to reprocess. </param>
	/// <param name="options"> Reprocessing options. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The result of the reprocessing operation. </returns>
	Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets statistics about the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Dead letter queue statistics. </returns>
	Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Purges all messages from the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The number of messages purged. </returns>
	Task<int> PurgeDeadLetterQueueAsync(
		CancellationToken cancellationToken);
}
