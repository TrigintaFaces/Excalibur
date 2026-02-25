// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the outbox dispatcher contract for reliable message publishing with transactional guarantees.
/// </summary>
/// <remarks>
/// The outbox dispatcher orchestrates background processing and publishing of messages stored in the outbox.
/// This is the orchestration layer interface for the outbox pattern, coordinating message dispatch to external systems.
/// <para> <strong> Pattern Benefits: </strong> </para>
/// - Guarantees at-least-once delivery semantics
/// - Maintains transactional consistency between data and messages
/// - Provides resilience against network failures and external system outages
/// - Enables idempotent message processing through deduplication
/// - Supports message ordering and retry mechanisms.
/// <para> <strong> Implementation Strategy: </strong> </para>
/// 1. Business operations save data and messages in a single database transaction
/// 2. A background dispatcher polls for pending messages and publishes them
/// 3. Successfully published messages are marked as processed or deleted
/// 4. Failed messages are retried with exponential backoff.
/// <para> <strong> Operational Considerations: </strong> </para>
/// - Requires reliable background processing for message dispatch
/// - Should implement appropriate retry policies and dead letter handling
/// - May need partitioning or sharding for high-throughput scenarios
/// - Monitoring and alerting are essential for operational visibility.
/// </remarks>
public interface IOutboxDispatcher : IAsyncDisposable
{
	/// <summary>
	/// Executes a single outbox dispatch cycle, processing pending messages for reliable delivery.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for the dispatcher instance to enable distributed processing coordination. </param>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns>
	/// A task representing the asynchronous operation, with the result containing the number of messages successfully processed during this
	/// dispatch cycle.
	/// </returns>
	/// <remarks>
	/// This method implements the core outbox processing logic by:
	/// <para> <strong> Processing Steps: </strong> </para>
	/// 1. Reserving a batch of pending messages using the dispatcher ID
	/// 2. Attempting to publish each reserved message to its target destination
	/// 3. Marking successfully published messages as processed or deleting them
	/// 4. Updating retry counts and next retry times for failed messages
	/// 5. Moving messages to dead letter storage after maximum retry attempts.
	/// <para> <strong> Concurrency and Distribution: </strong> </para>
	/// The dispatcher ID enables multiple instances to process the outbox concurrently without message duplication, supporting horizontal
	/// scaling and high availability.
	/// <para> <strong> Error Handling: </strong> </para>
	/// Transient failures are handled through retry mechanisms with exponential backoff, while permanent failures result in dead letter
	/// storage for manual intervention.
	/// </remarks>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled via the cancellation token. </exception>
	Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves integration events to the outbox within the current database transaction scope.
	/// </summary>
	/// <param name="integrationEvents"> Collection of integration events to be saved for eventual publishing. </param>
	/// <param name="metadata"> Metadata associated with the events including correlation IDs, timestamps, and routing information. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and cancellation support. </param>
	/// <returns> A task representing the asynchronous save operation. </returns>
	/// <remarks>
	/// This method stores integration events in the outbox table as part of the same database transaction that modifies business data,
	/// ensuring transactional consistency and preventing message loss.
	/// <para> <strong> Transactional Behavior: </strong> </para>
	/// The save operation must execute within an active database transaction context. If the transaction is rolled back, the saved events
	/// will also be rolled back, maintaining consistency between business operations and message publishing.
	/// <para> <strong> Event Processing: </strong> </para>
	/// Events are serialized and stored with appropriate metadata including:
	/// - Event type and version information for deserialization
	/// - Correlation and causation IDs for distributed tracing
	/// - Timestamps for processing order and debugging
	/// - Routing keys and headers for proper delivery.
	/// <para> <strong> Performance Considerations: </strong> </para>
	/// Batch saving multiple events in a single operation is more efficient than individual saves, reducing database round trips and
	/// transaction overhead.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when integrationEvents or metadata is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when not executed within an active transaction context. </exception>
	Task SaveEventsAsync(
		IReadOnlyCollection<IIntegrationEvent> integrationEvents,
		IMessageMetadata metadata,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves pre-constructed outbox messages directly to the outbox store.
	/// </summary>
	/// <param name="outboxMessages"> Collection of fully prepared outbox messages ready for storage. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and cancellation support. </param>
	/// <returns>
	/// A task representing the asynchronous operation, with the result containing the number of messages successfully saved to the outbox.
	/// </returns>
	/// <remarks>
	/// This method provides direct control over outbox message creation and storage, suitable for advanced scenarios where custom message
	/// formatting or routing logic is required.
	/// <para> <strong> Use Cases: </strong> </para>
	/// - Custom message serialization or encryption requirements
	/// - Complex routing scenarios with multiple destinations
	/// - Message transformation or enrichment logic
	/// - Integration with external message formats or protocols.
	/// <para> <strong> Message Validation: </strong> </para>
	/// The implementation should validate that messages have required fields populated including message body, type information, and
	/// routing details before storage.
	/// <para> <strong> Transactional Context: </strong> </para>
	/// Like SaveEventsAsync, this operation should execute within an active database transaction to maintain consistency with associated
	/// business operations.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when outboxMessages is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when outboxMessages contains invalid or incomplete message data. </exception>
	Task<int> SaveMessagesAsync(
		ICollection<IOutboxMessage> outboxMessages,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all pending messages from the outbox that are ready for dispatch.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and cancellation support. </param>
	/// <returns>
	/// A task representing the asynchronous operation, with the result containing an enumerable collection of pending dispatch messages.
	/// </returns>
	/// <remarks>
	/// This method provides read-only access to pending outbox messages for monitoring, diagnostics, and manual intervention scenarios. It
	/// does not modify message states or reserve messages for processing.
	/// <para> <strong> Message Selection Criteria: </strong> </para>
	/// Pending messages are typically those that:
	/// - Have not been successfully processed yet
	/// - Are not currently reserved by active dispatchers
	/// - Have not exceeded maximum retry limits
	/// - Are scheduled for immediate or past processing.
	/// <para> <strong> Operational Use Cases: </strong> </para>
	/// - Health monitoring and alerting on message queue depth
	/// - Troubleshooting and diagnostics for failed message processing
	/// - Manual message inspection and intervention
	/// - Metrics collection and performance analysis.
	/// <para> <strong> Performance Considerations: </strong> </para>
	/// This operation may return large result sets in high-throughput scenarios. Consider implementing pagination or filtering for
	/// production monitoring systems.
	/// </remarks>
	Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken);
}
