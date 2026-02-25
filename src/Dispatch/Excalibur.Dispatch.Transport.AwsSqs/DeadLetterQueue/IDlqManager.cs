// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Manages dead letter queue processing and recovery.
/// </summary>
public interface IDlqManager
{
	/// <summary>
	/// Processes a message from the dead letter queue.
	/// </summary>
	/// <param name="message"> The DLQ message to process. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The processing result. </returns>
	Task<DlqProcessingResult> ProcessMessageAsync(
		DlqMessage message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Moves a message to the dead letter queue.
	/// </summary>
	/// <param name="message"> The message to move to DLQ. </param>
	/// <param name="reason"> The reason for moving to DLQ. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> True if successful; otherwise, false. </returns>
	Task<bool> MoveToDeadLetterQueueAsync(
		DlqMessage message,
		string reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Redrives messages from the dead letter queue back to the source queue.
	/// </summary>
	/// <param name="messageIds"> Optional specific message IDs to redrive. </param>
	/// <param name="maxMessages"> Maximum number of messages to redrive. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The number of messages redriven. </returns>
	Task<int> RedriveMessagesAsync(
		CancellationToken cancellationToken,
		IEnumerable<string>? messageIds = null,
		int maxMessages = 10);

	/// <summary>
	/// Gets statistics about the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> DLQ statistics. </returns>
	Task<DlqStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
}
