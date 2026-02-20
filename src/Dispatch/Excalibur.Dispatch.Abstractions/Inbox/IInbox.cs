// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for inbox pattern implementations that provide exactly-once delivery guarantees and reliable message processing in
/// distributed systems. The inbox pattern ensures message idempotency by tracking processed messages and preventing duplicate processing.
/// </summary>
/// <remarks>
/// The inbox pattern is essential for building resilient distributed systems that can handle message delivery guarantees across service
/// boundaries. It provides deduplication capabilities and ensures that messages are processed exactly once, even in the presence of network
/// failures, retries, or system restarts. This pattern is particularly important for financial systems, order processing, and other
/// scenarios where duplicate processing could have serious business consequences.
/// </remarks>
public interface IInbox : IAsyncDisposable
{
	/// <summary>
	/// Asynchronously processes all pending messages in the inbox for the specified dispatcher, ensuring exactly-once delivery semantics
	/// and proper message ordering where required.
	/// </summary>
	/// <param name="dispatcherId">
	/// The unique identifier of the dispatcher instance processing messages, used for coordination and preventing concurrent processing
	/// conflicts in multi-instance deployments.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during inbox processing. </param>
	/// <returns>
	/// A task that represents the asynchronous inbox processing operation, containing the number of messages successfully processed during
	/// this execution.
	/// </returns>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="dispatcherId" /> is null, empty, or whitespace. </exception>
	/// <remarks>
	/// This method implements the core inbox processing logic, handling message deduplication, ordering constraints, and error recovery. It
	/// processes messages in a transactionally safe manner, ensuring that message processing and inbox state updates are atomic. Failed
	/// message processing may result in messages being retried according to configured retry policies.
	/// </remarks>
	Task<int> RunInboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken);

	/// <summary>
	/// Asynchronously saves a message to the inbox with deduplication tracking, ensuring that duplicate messages (identified by external
	/// ID) are not processed multiple times.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message to save, must implement <see cref="IDispatchMessage" />. </typeparam>
	/// <param name="message"> The message instance to save for future processing. </param>
	/// <param name="externalId">
	/// A unique external identifier for the message, typically from the originating system, used for deduplication and idempotency checking.
	/// </param>
	/// <param name="metadata">
	/// Message metadata containing correlation information, timestamps, and other contextual data needed for proper message processing and tracing.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during save operations. </param>
	/// <returns> A task that represents the asynchronous message save operation. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message" />, <paramref name="externalId" />, or <paramref name="metadata" /> is null.
	/// </exception>
	/// <remarks>
	/// This method implements the message storage aspect of the inbox pattern, ensuring that incoming messages are durably persisted with
	/// proper deduplication tracking. If a message with the same external ID has already been processed, the implementation may choose to
	/// ignore the duplicate or handle it according to configured duplicate message policies. The method should be transactionally safe and
	/// handle concurrent message arrival scenarios appropriately.
	/// </remarks>
	Task SaveMessageAsync<TMessage>(TMessage message, string externalId, IMessageMetadata metadata,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage;
}
