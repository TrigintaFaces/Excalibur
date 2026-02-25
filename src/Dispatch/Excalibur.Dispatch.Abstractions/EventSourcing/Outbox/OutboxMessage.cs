// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a message stored in the transactional outbox.
///
/// <remarks>
/// <para>
/// The outbox message contains the full payload (JSON-serialized domain event) for reliable
/// publishing and audit trail.
/// </para>
///
/// <para>
/// <strong>Storage Strategy:</strong> Full payload is stored to ensure reliable publishing
/// even if the original event is deleted or modified.
/// </para>
///
/// <para>
/// <strong>Lifecycle:</strong>
/// <list type="number">
/// <item>Created with PublishedAt = null (pending)</item>
/// <item>Retrieved by background service via <see cref="IOutboxStore.GetPendingAsync"/></item>
/// <item>Published to message bus</item>
/// <item>Marked as published via <see cref="IOutboxStore.MarkAsPublishedAsync"/> (PublishedAt = UTC timestamp)</item>
/// <item>Retained for audit (default: 7 days)</item>
/// <item>Deleted via <see cref="IOutboxStore.DeletePublishedOlderThanAsync"/></item>
/// </list>
/// </para>
///
/// <para>
/// <strong>Thread Safety:</strong> This record is immutable and thread-safe.
/// </para>
/// </remarks>
/// </summary>
public sealed record OutboxMessage
{
	/// <summary>
	/// Gets the unique identifier of the outbox message.
	///
	/// <para>
	/// <strong>Generation:</strong> Use <see cref="Guid.NewGuid()"/> when creating new messages.
	/// </para>
	/// </summary>
	public required Guid Id { get; init; }

	/// <summary>
	/// Gets the unique identifier of the aggregate that generated the event.
	///
	/// <para>
	/// Used for correlation and filtering in monitoring/audit scenarios.
	/// </para>
	/// </summary>
	public required string AggregateId { get; init; }

	/// <summary>
	/// Gets the type of the aggregate that generated the event.
	///
	/// <para>
	/// Example: "Order", "Customer", "Inventory"
	/// </para>
	/// </summary>
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets the type of the domain event.
	///
	/// <para>
	/// Example: "OrderCreated", "CustomerRegistered", "InventoryAdjusted"
	/// </para>
	///
	/// <para>
	/// Used for routing and filtering in message bus subscribers.
	/// </para>
	/// </summary>
	public required string EventType { get; init; }

	/// <summary>
	/// Gets the JSON-serialized payload of the domain event.
	///
	/// <para>
	/// <strong>Format:</strong> JSON string containing the full domain event data.
	/// </para>
	///
	/// <para>
	/// <strong>Serialization:</strong> Use System.Text.Json for consistency with framework defaults.
	/// </para>
	/// </summary>
	public required string EventData { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was created (added to outbox).
	///
	/// <para>
	/// Used for FIFO ordering in <see cref="IOutboxStore.GetPendingAsync"/> (ORDER BY CreatedAt ASC).
	/// </para>
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was successfully published to the message bus.
	///
	/// <para>
	/// <strong>Null:</strong> Message is pending publication.
	/// </para>
	///
	/// <para>
	/// <strong>Not Null:</strong> Message has been published successfully.
	/// </para>
	///
	/// <para>
	/// Used for idempotency guards (WHERE PublishedAt IS NULL) and cleanup queries.
	/// </para>
	/// </summary>
	public DateTimeOffset? PublishedAt { get; init; }

	/// <summary>
	/// Gets the number of times the message publishing has been attempted.
	///
	/// <para>
	/// Incremented by <see cref="IOutboxStore.IncrementRetryCountAsync"/> when publishing fails.
	/// </para>
	///
	/// <para>
	/// Can be used to implement:
	/// <list type="bullet">
	/// <item>Exponential backoff (delay = baseDelay * 2^retryCount)</item>
	/// <item>Dead-letter queue (move to DLQ after N retries)</item>
	/// <item>Alerting (notify ops when retryCount > threshold)</item>
	/// </list>
	/// </para>
	/// </summary>
	public int RetryCount { get; init; }

	/// <summary>
	/// Gets the message type for routing and filtering.
	///
	/// <para>
	/// Example: "DomainEvent", "IntegrationEvent", "Command"
	/// </para>
	///
	/// <para>
	/// Used by message bus publishers to determine routing strategy.
	/// </para>
	/// </summary>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets optional metadata for the message (e.g., correlation ID, causation ID, user ID).
	///
	/// <para>
	/// <strong>Format:</strong> JSON string containing key-value pairs.
	/// </para>
	///
	/// <para>
	/// Example: {"CorrelationId": "abc-123", "CausationId": "xyz-789", "UserId": "user-456"}
	/// </para>
	///
	/// <para>
	/// <strong>Nullable:</strong> Can be null if no metadata is needed.
	/// </para>
	/// </summary>
	public string? Metadata { get; init; }
}
