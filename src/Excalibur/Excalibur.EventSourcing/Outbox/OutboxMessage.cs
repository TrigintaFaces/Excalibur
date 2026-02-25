// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Represents a message stored in the transactional outbox for event-sourced systems.
/// </summary>
/// <remarks>
/// <para>
/// The outbox message contains the full payload (JSON-serialized domain event) for reliable
/// publishing and audit trail.
/// </para>
/// <para>
/// <strong>Lifecycle:</strong>
/// <list type="number">
/// <item>Created with PublishedAt = null (pending)</item>
/// <item>Retrieved by background service via <see cref="IEventSourcedOutboxStore.GetPendingAsync"/></item>
/// <item>Published to message bus</item>
/// <item>Marked as published via <see cref="IEventSourcedOutboxStore.MarkAsPublishedAsync"/> (PublishedAt = UTC timestamp)</item>
/// <item>Retained for audit (default: 7 days)</item>
/// <item>Deleted via <see cref="IEventSourcedOutboxStore.DeletePublishedOlderThanAsync"/></item>
/// </list>
/// </para>
/// </remarks>
public sealed record OutboxMessage
{
	/// <summary>
	/// Gets the unique identifier of the outbox message.
	/// </summary>
	public required Guid Id { get; init; }

	/// <summary>
	/// Gets the unique identifier of the aggregate that generated the event.
	/// </summary>
	public required string AggregateId { get; init; }

	/// <summary>
	/// Gets the type of the aggregate that generated the event.
	/// </summary>
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets the type of the domain event.
	/// </summary>
	public required string EventType { get; init; }

	/// <summary>
	/// Gets the JSON-serialized payload of the domain event.
	/// </summary>
	public required string EventData { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was created (added to outbox).
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was successfully published.
	/// Null indicates the message is pending publication.
	/// </summary>
	public DateTimeOffset? PublishedAt { get; init; }

	/// <summary>
	/// Gets the number of times the message publishing has been attempted.
	/// </summary>
	public int RetryCount { get; init; }

	/// <summary>
	/// Gets the message type for routing and filtering.
	/// </summary>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets optional metadata for the message (e.g., correlation ID, causation ID).
	/// </summary>
	public string? Metadata { get; init; }
}
