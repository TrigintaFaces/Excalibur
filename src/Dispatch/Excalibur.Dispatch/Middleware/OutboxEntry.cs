// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents an entry in the outbox for staged messages.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="OutboxEntry" /> class. </remarks>
public sealed class OutboxEntry(
	string id,
	string messageType,
	string messageData,
	string? correlationId,
	string? causationId,
	string? tenantId,
	string? destination,
	DateTimeOffset scheduledAt,
	DateTimeOffset createdAt)
{
	/// <summary>
	/// Gets the unique identifier for the outbox entry.
	/// </summary>
	/// <value>
	/// The unique identifier for the outbox entry.
	/// </value>
	public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));

	/// <summary>
	/// Gets the message type name.
	/// </summary>
	/// <value>
	/// The message type name.
	/// </value>
	public string MessageType { get; } = messageType ?? throw new ArgumentNullException(nameof(messageType));

	/// <summary>
	/// Gets the serialized message data.
	/// </summary>
	/// <value>
	/// The serialized message data.
	/// </value>
	public string MessageData { get; } = messageData ?? throw new ArgumentNullException(nameof(messageData));

	/// <summary>
	/// Gets the correlation identifier.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets the causation identifier.
	/// </summary>
	/// <value>The current <see cref="CausationId"/> value.</value>
	public string? CausationId { get; } = causationId;

	/// <summary>
	/// Gets the tenant identifier.
	/// </summary>
	/// <value>The current <see cref="TenantId"/> value.</value>
	public string? TenantId { get; } = tenantId;

	/// <summary>
	/// Gets the destination for the message.
	/// </summary>
	/// <value>The current <see cref="Destination"/> value.</value>
	public string? Destination { get; } = destination;

	/// <summary>
	/// Gets the scheduled delivery time.
	/// </summary>
	/// <value>The current <see cref="ScheduledAt"/> value.</value>
	public DateTimeOffset ScheduledAt { get; } = scheduledAt;

	/// <summary>
	/// Gets the creation time.
	/// </summary>
	/// <value>The current <see cref="CreatedAt"/> value.</value>
	public DateTimeOffset CreatedAt { get; } = createdAt;
}
