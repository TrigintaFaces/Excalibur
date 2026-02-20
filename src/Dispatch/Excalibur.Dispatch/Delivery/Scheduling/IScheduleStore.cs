// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines the contract for persistent storage and retrieval of scheduled messages with lifecycle management.
/// </summary>
/// <remarks>
/// The schedule store is the persistence layer for the scheduling system, providing durable storage that survives system restarts,
/// deployments, and failures. It enables reliable scheduled message delivery with strong consistency guarantees and efficient query capabilities.
/// <para> <strong> Storage Requirements: </strong> </para>
/// Implementations must provide:
/// - ACID transaction support for consistent schedule operations
/// - Efficient querying by next execution time for scheduler polling
/// - Concurrent access support for distributed scheduler instances
/// - Backup and recovery capabilities for business continuity.
/// <para> <strong> Performance Characteristics: </strong> </para>
/// The store should be optimized for:
/// - Fast writes for high-volume schedule creation
/// - Efficient range queries for upcoming executions
/// - Minimal lock contention during concurrent operations
/// - Scalable storage for millions of scheduled messages.
/// <para> <strong> Implementation Patterns: </strong> </para>
/// Common implementation approaches include:
/// - Database-backed stores with optimized indexes
/// - Time-series databases for high-volume scenarios
/// - Distributed storage systems for multi-region deployments
/// - Hybrid approaches combining fast access with durable persistence.
/// </remarks>
public interface IScheduleStore
{
	/// <summary>
	/// Retrieves all scheduled messages from the store for processing and management.
	/// </summary>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the retrieval operation. </param>
	/// <returns>
	/// A task representing the asynchronous operation, with the result containing an enumerable collection of all scheduled messages in the store.
	/// </returns>
	/// <remarks>
	/// This method provides access to the complete schedule inventory for various scenarios:
	/// <para> <strong> Use Cases: </strong> </para>
	/// - Scheduler initialization and full schedule discovery
	/// - Administrative tools for schedule management and monitoring
	/// - Migration and backup operations
	/// - Debugging and troubleshooting scheduled message issues.
	/// <para> <strong> Performance Considerations: </strong> </para>
	/// This method may return large result sets in production environments with many schedules. Implementations should consider:
	/// - Streaming results for memory efficiency
	/// - Pagination support for large datasets
	/// - Read-only snapshots for consistency
	/// - Connection pooling and timeout handling.
	/// <para> <strong> Filtering and Ordering: </strong> </para>
	/// Results are typically ordered by next execution time to optimize scheduler processing, though specific ordering guarantees depend on
	/// the implementation.
	/// </remarks>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the store is in an invalid state for reading. </exception>
	Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stores a scheduled message in the persistent store for future execution.
	/// </summary>
	/// <param name="message"> The scheduled message to store. Cannot be null and must have a valid ID. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the store operation. </param>
	/// <returns> A task representing the asynchronous storage operation. </returns>
	/// <remarks>
	/// This method provides idempotent storage for scheduled messages, handling both new schedule creation and updates to existing
	/// schedules based on the message ID.
	/// <para> <strong> Storage Behavior: </strong> </para>
	/// The implementation should:
	/// - Create new schedules for unknown IDs
	/// - Update existing schedules for known IDs (upsert semantics)
	/// - Validate message data integrity before storage
	/// - Ensure atomic operations for consistency.
	/// <para> <strong> Data Validation: </strong> </para>
	/// Before storage, the implementation should validate:
	/// - Required fields are populated (ID, MessageBody, MessageName)
	/// - Cron expressions are valid if provided
	/// - Timezone IDs are recognized if specified
	/// - Next execution times are calculated correctly.
	/// <para> <strong> Concurrency Handling: </strong> </para>
	/// The store must handle concurrent access scenarios including:
	/// - Multiple scheduler instances updating the same schedule
	/// - Administrative tools modifying schedules during execution
	/// - Bulk operations competing with individual updates.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when message is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when message contains invalid data. </exception>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the store is in an invalid state for writing. </exception>
	Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a scheduled message as completed and removes it from active scheduling.
	/// </summary>
	/// <param name="scheduleId"> The unique identifier of the scheduled message to complete. </param>
	/// <param name="cancellationToken"> Token to observe for cancellation requests during the completion operation. </param>
	/// <returns> A task representing the asynchronous completion operation. </returns>
	/// <remarks>
	/// This method handles the lifecycle completion of scheduled messages, typically called after successful message execution or when a
	/// schedule should no longer be processed.
	/// <para> <strong> Completion Scenarios: </strong> </para>
	/// Schedules are completed in various situations:
	/// - One-time schedules after successful execution
	/// - Recurring schedules that have reached their end condition
	/// - Schedules explicitly cancelled by administrative action
	/// - Failed schedules that have exhausted retry attempts.
	/// <para> <strong> Data Handling: </strong> </para>
	/// Completion behavior depends on implementation requirements:
	/// - Hard delete: Permanently remove the schedule record
	/// - Soft delete: Mark as inactive while preserving for audit
	/// - Archive: Move to historical storage for compliance
	/// - Cascade: Handle related execution history and metadata.
	/// <para> <strong> Idempotency: </strong> </para>
	/// This operation should be idempotent - completing an already completed or non-existent schedule should not result in errors, enabling
	/// safe retry scenarios and distributed processing coordination.
	/// </remarks>
	/// <exception cref="OperationCanceledException"> Thrown when the operation is cancelled. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the store is in an invalid state for completion operations. </exception>
	Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken);
}
