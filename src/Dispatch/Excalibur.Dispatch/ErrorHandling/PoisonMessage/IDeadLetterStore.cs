// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Defines the contract for storing and retrieving messages from the dead letter queue.
/// </summary>
public interface IDeadLetterStore
{
	/// <summary>
	/// Stores a message in the dead letter queue.
	/// </summary>
	/// <param name="message"> The dead letter message to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a dead letter message by its ID.
	/// </summary>
	/// <param name="messageId"> The ID of the message to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the dead letter message, or null if not found. </returns>
	Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves dead letter messages based on filter criteria.
	/// </summary>
	/// <param name="filter"> The filter criteria for retrieving messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the collection of matching dead letter messages. </returns>
	Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a dead letter message as replayed.
	/// </summary>
	/// <param name="messageId"> The ID of the message that was replayed. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a dead letter message.
	/// </summary>
	/// <param name="messageId"> The ID of the message to delete. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation with a boolean indicating success. </returns>
	Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the count of messages in the dead letter queue.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the count of messages. </returns>
	Task<long> GetCountAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Cleans up old dead letter messages based on retention policy.
	/// </summary>
	/// <param name="retentionDays"> The number of days to retain messages. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task containing the number of messages cleaned up. </returns>
	Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken);
}
