// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Represents a message stored in the outbox pattern for guaranteed delivery. The outbox pattern ensures that messages are persisted before
/// being dispatched to prevent message loss in case of system failures.
/// </summary>
public interface IOutboxMessage
{
	/// <summary>
	/// Gets the unique identifier for the message.
	/// </summary>
	/// <value>
	/// The unique identifier for the message.
	/// </value>
	string MessageId { get; init; }

	/// <summary>
	/// Gets the type of the message, typically used for deserialization and routing purposes.
	/// </summary>
	/// <value>
	/// The type of the message, typically used for deserialization and routing purposes.
	/// </value>
	string MessageType { get; init; }

	/// <summary>
	/// Gets the metadata associated with the message, such as headers, correlation IDs, and other routing information.
	/// </summary>
	/// <value>
	/// The metadata associated with the message, such as headers, correlation IDs, and other routing information.
	/// </value>
	string MessageMetadata { get; init; }

	/// <summary>
	/// Gets the serialized body content of the message as raw bytes.
	/// </summary>
	/// <remarks>
	/// The body is stored as <see cref="T:System.Byte" />[] so arbitrary payloads — binary, compressed,
	/// or encrypted — round-trip losslessly. A string body would corrupt any non-UTF8 content, matching
	/// how first-party message models carry byte bodies (Azure Service Bus <c>ServiceBusMessage.Body</c>,
	/// Event Hubs <c>EventData.Body</c>, Kafka payloads).
	/// </remarks>
	/// <value>
	/// The serialized body content of the message.
	/// </value>
	byte[] MessageBody { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was created and stored in the outbox.
	/// </summary>
	/// <value>
	/// The timestamp when the message was created and stored in the outbox.
	/// </value>
	DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets or sets the optional expiration timestamp for the message. Messages past their expiration time may be discarded.
	/// </summary>
	/// <value>
	/// The optional expiration timestamp for the message. Messages past their expiration time may be discarded.
	/// </value>
	DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts made for this message. Used for retry logic and dead letter queue processing.
	/// </summary>
	/// <value>
	/// The number of delivery attempts made for this message. Used for retry logic and dead letter queue processing.
	/// </value>
	int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the dispatcher currently processing this message. Used for distributed processing coordination and
	/// preventing duplicate processing.
	/// </summary>
	/// <value>
	/// The identifier of the dispatcher currently processing this message. Used for distributed processing coordination and
	/// preventing duplicate processing.
	/// </value>
	string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the timeout timestamp for the current dispatcher processing this message. If the dispatcher exceeds this timeout, the
	/// message can be reassigned to another dispatcher.
	/// </summary>
	/// <value>
	/// The timeout timestamp for the current dispatcher processing this message. If the dispatcher exceeds this timeout, the
	/// message can be reassigned to another dispatcher.
	/// </value>
	DateTimeOffset? DispatcherTimeout { get; set; }

	/// <summary>
	/// Gets the tenant identifier this message was produced under, for multi-tenant scope on the persisted
	/// outbox row. Defaults to <see langword="null"/> so existing providers that do not yet persist tenant
	/// scope inherit the default unchanged; a provider with a tenant column overrides this to surface the
	/// stored value (e.g. the Postgres outbox store). Making tenant expressible on the row contract keeps a
	/// provider from silently re-dropping it.
	/// </summary>
	/// <value>The tenant identifier, or <see langword="null"/> when the store does not carry tenant scope.</value>
	string? TenantId => null;

	/// <summary>
	/// Gets the delivery destination this message is routed to, for round-trip parity on the persisted
	/// outbox row. Defaults to <see langword="null"/> so existing providers that do not yet persist the
	/// destination inherit the default unchanged; a provider with a destination column overrides this to
	/// surface the stored value. Making destination expressible on the row contract keeps a provider from
	/// silently dropping it on the stage-then-reload path.
	/// </summary>
	/// <value>The delivery destination, or <see langword="null"/> when the store does not carry it.</value>
	string? Destination => null;

	/// <summary>
	/// Gets the delivery priority (higher values are dispatched first) on the persisted outbox row. Defaults
	/// to <c>0</c> so existing providers that do not yet persist a priority inherit the default unchanged; a
	/// provider with a priority column overrides this to surface the stored value. Making priority
	/// expressible on the row contract keeps a provider from silently dropping it on the stage-then-reload path.
	/// </summary>
	/// <value>The delivery priority; <c>0</c> when the store does not carry it.</value>
	int Priority => 0;

	/// <summary>
	/// Gets the time before which the message must not be delivered, for schedule parity on the persisted
	/// outbox row. Defaults to <see langword="null"/> (immediately deliverable) so existing providers that do
	/// not yet persist a schedule inherit the default unchanged; a provider with a scheduled-at column
	/// overrides this to surface the stored value. Making the schedule expressible on the row contract keeps a
	/// provider from silently treating a future-scheduled message as immediately deliverable after a reload.
	/// </summary>
	/// <value>The scheduled delivery time, or <see langword="null"/> when the message is deliverable immediately.</value>
	DateTimeOffset? ScheduledAt => null;
}
