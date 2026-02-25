// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a contract for implementing message deduplication strategies.
/// </summary>
public interface IDeduplicationStrategy
{
	/// <summary>
	/// Gets the default expiration time for deduplication records.
	/// </summary>
	/// <value> The duration deduplication entries remain valid. </value>
	TimeSpan DefaultExpiration { get; }

	/// <summary>
	/// Generates a unique identifier for deduplication based on the message content.
	/// </summary>
	/// <param name="messageBody"> The message body. </param>
	/// <param name="messageAttributes"> Optional message attributes. </param>
	/// <returns> A unique identifier for deduplication. </returns>
	string GenerateDeduplicationId(string messageBody, IDictionary<string, object>? messageAttributes = null);

	/// <summary>
	/// Checks if a message has already been processed based on its deduplication ID.
	/// </summary>
	/// <param name="deduplicationId"> The deduplication identifier. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> True if the message has been processed; otherwise, false. </returns>
	Task<bool> IsDuplicateAsync(string deduplicationId, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as processed for deduplication purposes.
	/// </summary>
	/// <param name="deduplicationId"> The deduplication identifier. </param>
	/// <param name="expiration"> Optional expiration time for the deduplication record. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	Task MarkAsProcessedAsync(string deduplicationId, TimeSpan? expiration, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a deduplication record.
	/// </summary>
	/// <param name="deduplicationId"> The deduplication identifier. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> True if the record was removed; otherwise, false. </returns>
	Task<bool> RemoveAsync(string deduplicationId, CancellationToken cancellationToken);

	/// <summary>
	/// Generates a unique identifier for deduplication asynchronously.
	/// </summary>
	/// <param name="messageBody"> The message body. </param>
	/// <param name="messageAttributes"> Optional message attributes. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A unique identifier for deduplication. </returns>
	Task<string> GenerateIdAsync(string messageBody, IDictionary<string, object>? messageAttributes,
		CancellationToken cancellationToken);
}
