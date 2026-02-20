// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for inbox message processors that handle the execution and lifecycle management of inbox pattern implementations.
/// Processors coordinate between inbox storage and the message dispatching infrastructure to ensure reliable message processing.
/// </summary>
/// <remarks>
/// Inbox processors implement the processing logic for the inbox pattern, managing the coordination between message storage, deduplication
/// tracking, and actual message dispatch. They handle batching, error recovery, and resource management while ensuring that messages are
/// processed exactly once according to the inbox pattern guarantees.
/// </remarks>
public interface IInboxProcessor : IAsyncDisposable
{
	/// <summary>
	/// Initializes the inbox processor with the specified dispatcher identifier for coordination and state management in multi-instance deployments.
	/// </summary>
	/// <param name="dispatcherId">
	/// The unique identifier of the dispatcher instance that will be processing messages, used for coordination and preventing conflicts
	/// between multiple processor instances.
	/// </param>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="dispatcherId" /> is null, empty, or whitespace. </exception>
	/// <remarks>
	/// This method prepares the processor for message processing operations by establishing the dispatcher identity and any necessary state
	/// management structures. The dispatcher ID is used to coordinate processing in scenarios where multiple instances might be running
	/// concurrently, ensuring proper message ownership and preventing duplicate processing.
	/// </remarks>
	void Init(string dispatcherId);

	/// <summary>
	/// Asynchronously processes all pending messages in the inbox, dispatching them through the appropriate message handlers while
	/// maintaining exactly-once delivery semantics.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during message processing. </param>
	/// <returns>
	/// A task that represents the asynchronous processing operation, containing the number of messages successfully processed during this
	/// execution batch.
	/// </returns>
	/// <remarks>
	/// This method implements the core processing loop for the inbox pattern, handling message retrieval, deduplication checking, dispatch
	/// coordination, and state updates. It processes messages in batches according to configured limits and ensures that processing is
	/// transactionally safe. Failed messages may be retried according to configured retry policies, and the method coordinates with the
	/// inbox storage to update message processing status.
	/// </remarks>
	Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken);
}
