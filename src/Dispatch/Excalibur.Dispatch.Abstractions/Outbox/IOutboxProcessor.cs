// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for outbox pattern message processing, enabling reliable message delivery with transactional guarantees. The outbox
/// processor handles deferred message publishing to ensure consistency between business operations and message delivery. This pattern
/// prevents message loss and ensures at-least-once delivery semantics in distributed systems.
/// </summary>
public interface IOutboxProcessor : IAsyncDisposable
{
	/// <summary>
	/// Initializes the outbox processor with a unique dispatcher identifier for message ownership and processing coordination. This method
	/// sets up the processor context and prepares it for message processing operations.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance, used for message ownership and coordination. </param>
	void Init(string dispatcherId);

	/// <summary>
	/// Processes and dispatches all pending messages from the outbox to their destination message brokers. This method implements the core
	/// outbox pattern functionality, ensuring transactional message delivery with proper error handling, retry logic, and delivery confirmation.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task containing the number of messages successfully dispatched from the outbox. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the processor is not properly initialized. </exception>
	Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken);
}
