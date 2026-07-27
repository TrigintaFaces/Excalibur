// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

namespace Excalibur.Outbox;

/// <summary>
/// Represents a message stored in the outbox pattern for reliable message delivery.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public record OutboxMessage : IOutboxMessage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMessage" /> class.
	/// </summary>
	public OutboxMessage()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMessage" /> class with required properties.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="createdAt"> The timestamp when the message was created. </param>
	[SetsRequiredMembers]
	internal OutboxMessage(string messageId, string messageType, string messageMetadata, byte[] messageBody, DateTimeOffset createdAt)
	{
		MessageId = messageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		CreatedAt = createdAt;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMessage" /> class with expiration.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="createdAt"> The timestamp when the message was created. </param>
	/// <param name="expiresAt"> The optional expiration timestamp for the message. </param>
	[SetsRequiredMembers]
	internal OutboxMessage(
		string messageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		DateTimeOffset createdAt,
		DateTimeOffset? expiresAt)
	{
		MessageId = messageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		CreatedAt = createdAt;
		ExpiresAt = expiresAt;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMessage" /> class with dispatch tracking.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="createdAt"> The timestamp when the message was created. </param>
	/// <param name="attempts"> The number of dispatch attempts. </param>
	/// <param name="dispatcherId"> The optional identifier of the dispatcher handling this message. </param>
	/// <param name="dispatcherTimeout"> The optional timeout for the dispatcher. </param>
	[SetsRequiredMembers]
	internal OutboxMessage(string messageId, string messageType, string messageMetadata, byte[] messageBody, DateTimeOffset createdAt,
		int attempts, string? dispatcherId, DateTimeOffset? dispatcherTimeout)
	{
		MessageId = messageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		CreatedAt = createdAt;
		Attempts = attempts;
		DispatcherId = dispatcherId;
		DispatcherTimeout = dispatcherTimeout;
	}

	/// <summary>
	/// Creates an <see cref="OutboxMessage"/> from an <see cref="Excalibur.Dispatch.OutboundMessage"/>, carrying the
	/// common fields — including <see cref="TenantId"/> — in one place so no provider stage path silently drops
	/// tenant scope during the <c>OutboundMessage → OutboxMessage</c> conversion.
	/// </summary>
	/// <remarks>
	/// This is the canonical conversion: provider outbox stores route their stage paths through it rather than
	/// re-inlining the bare constructor (which omits <see cref="TenantId"/>). It owns the common-field mapping
	/// (id, type, headers, payload, created-at, tenant).
	/// </remarks>
	/// <param name="message">The outbound message to convert.</param>
	/// <returns>An outbox message carrying the outbound message's id, type, headers, payload, and tenant.</returns>
	public static OutboxMessage FromOutboundMessage(Excalibur.Dispatch.OutboundMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
		var headersJson = System.Text.Json.JsonSerializer.Serialize(
			message.Headers ?? new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal),
			Excalibur.Dispatch.EventSerializationDefaults.Canonical);
#pragma warning restore IL2026, IL3050

		return new OutboxMessage(
			message.Id,
			message.MessageType,
			headersJson,
			message.Payload,
			message.CreatedAt)
		{
			TenantId = message.TenantId,
			Destination = message.Destination,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			Priority = message.Priority,
			ScheduledAt = message.ScheduledAt,
			PartitionKey = message.PartitionKey,
			GroupKey = message.GroupKey,
			SequenceNumber = message.SequenceNumber,
			TargetTransports = message.TargetTransports,
			IsMultiTransport = message.IsMultiTransport,
		};
	}

	/// <summary>
	/// Gets the unique identifier for the message.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public required string MessageId { get; init; }

	/// <summary>
	/// Gets the type of the message.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets the serialized message metadata.
	/// </summary>
	/// <value>The current <see cref="MessageMetadata"/> value.</value>
	public required string MessageMetadata { get; init; }

	/// <summary>
	/// Gets the serialized message body as raw bytes.
	/// </summary>
	/// <remarks>
	/// Stored as <see cref="T:System.Byte" />[] so binary, compressed, or encrypted payloads round-trip
	/// losslessly — a string body would corrupt any non-UTF8 content.
	/// </remarks>
	/// <value>The current <see cref="MessageBody"/> value.</value>
	public required byte[] MessageBody { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was created.
	/// </summary>
	/// <value>The current <see cref="CreatedAt"/> value.</value>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets or sets the optional expiration timestamp for the message.
	/// </summary>
	/// <value>The current <see cref="ExpiresAt"/> value.</value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of dispatch attempts made for this message.
	/// </summary>
	/// <value>The current <see cref="Attempts"/> value.</value>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the optional identifier of the dispatcher handling this message.
	/// </summary>
	/// <value>The current <see cref="DispatcherId"/> value.</value>
	public string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the optional timeout for the dispatcher.
	/// </summary>
	/// <value>The current <see cref="DispatcherTimeout"/> value.</value>
	public DateTimeOffset? DispatcherTimeout { get; set; }

	/// <summary>
	/// Gets the tenant identifier this message was produced under (overrides the
	/// <see cref="IOutboxMessage.TenantId"/> default so the Postgres outbox store's persisted
	/// <c>tenant_id</c> column round-trips).
	/// </summary>
	/// <value>The tenant identifier, or <see langword="null"/> when no tenant scope was carried.</value>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the delivery destination this message is routed to, carried on the persisted outbox row so it
	/// round-trips through the stage-then-reload path rather than being silently dropped.
	/// </summary>
	/// <value>The delivery destination, or <see langword="null"/> when none was carried.</value>
	public string? Destination { get; init; }

	/// <summary>
	/// Gets the correlation identifier grouping this message with the wider business operation, carried on the
	/// persisted row so it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The correlation identifier, or <see langword="null"/> when none was carried.</value>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets the causation identifier of the message that caused this one, carried on the persisted row so it
	/// round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The causation identifier, or <see langword="null"/> when none was carried.</value>
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets the delivery priority (higher values indicate higher priority), carried on the persisted row so
	/// it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The priority level; <c>0</c> when none was specified.</value>
	public int Priority { get; init; }

	/// <summary>
	/// Gets the time before which the message must not be delivered, carried on the persisted row so a
	/// future-scheduled message is not treated as immediately deliverable after a stage-then-reload.
	/// </summary>
	/// <value>The scheduled delivery time, or <see langword="null"/> when the message is deliverable immediately.</value>
	public DateTimeOffset? ScheduledAt { get; init; }

	/// <summary>
	/// Gets the partition key used to preserve ordered delivery within a partition, carried on the persisted
	/// row so it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The partition key, or <see langword="null"/> when none was carried.</value>
	public string? PartitionKey { get; init; }

	/// <summary>
	/// Gets the group key used for logical message grouping, carried on the persisted row so it round-trips
	/// through the stage-then-reload path.
	/// </summary>
	/// <value>The group key, or <see langword="null"/> when none was carried.</value>
	public string? GroupKey { get; init; }

	/// <summary>
	/// Gets the sequence number enforcing ascending in-partition delivery order, carried on the persisted row
	/// so it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The sequence number, or <c>0</c> when none was carried.</value>
	public long SequenceNumber { get; init; }

	/// <summary>
	/// Gets the comma-separated set of target transports this message is routed to, carried on the persisted
	/// row so it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value>The target transports, or <see langword="null"/> when none was carried.</value>
	public string? TargetTransports { get; init; }

	/// <summary>
	/// Gets a value indicating whether this message is delivered to multiple transports, carried on the
	/// persisted row so it round-trips through the stage-then-reload path.
	/// </summary>
	/// <value><see langword="true"/> when the message targets multiple transports; otherwise <see langword="false"/>.</value>
	public bool IsMultiTransport { get; init; }

	/// <summary>
	/// Determines whether the specified <see cref="OutboxMessage"/> is equal to the current instance,
	/// comparing <see cref="MessageBody"/> by <em>content</em> rather than by reference.
	/// </summary>
	/// <remarks>
	/// The compiler-synthesized record equality compares the <see cref="MessageBody"/> byte array by
	/// reference, which would make two records carrying identical payloads unequal. This override restores
	/// value semantics by comparing the payload bytes structurally, keeping the record's value-equality
	/// contract intact for a binary body.
	/// </remarks>
	/// <param name="other">The other message to compare against.</param>
	/// <returns><see langword="true"/> if the messages are equal; otherwise <see langword="false"/>.</returns>
	public virtual bool Equals(OutboxMessage? other) =>
		other is not null
		&& EqualityContract == other.EqualityContract
		&& MessageId == other.MessageId
		&& MessageType == other.MessageType
		&& MessageMetadata == other.MessageMetadata
		&& MessageBodyEquals(MessageBody, other.MessageBody)
		&& CreatedAt == other.CreatedAt
		&& ExpiresAt == other.ExpiresAt
		&& Attempts == other.Attempts
		&& DispatcherId == other.DispatcherId
		&& DispatcherTimeout == other.DispatcherTimeout
		&& TenantId == other.TenantId
		&& Destination == other.Destination
		&& CorrelationId == other.CorrelationId
		&& CausationId == other.CausationId
		&& PartitionKey == other.PartitionKey
		&& GroupKey == other.GroupKey
		&& SequenceNumber == other.SequenceNumber
		&& TargetTransports == other.TargetTransports
		&& IsMultiTransport == other.IsMultiTransport;

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(EqualityContract);
		hash.Add(MessageId);
		hash.Add(MessageType);
		hash.Add(MessageMetadata);
		if (MessageBody is not null)
		{
			hash.AddBytes(MessageBody);
		}

		hash.Add(CreatedAt);
		hash.Add(ExpiresAt);
		hash.Add(Attempts);
		hash.Add(DispatcherId);
		hash.Add(DispatcherTimeout);
		hash.Add(TenantId);
		hash.Add(Destination);
		hash.Add(CorrelationId);
		hash.Add(CausationId);
		hash.Add(PartitionKey);
		hash.Add(GroupKey);
		hash.Add(SequenceNumber);
		hash.Add(TargetTransports);
		hash.Add(IsMultiTransport);
		return hash.ToHashCode();
	}

	// MessageBody is declared non-null (required), but Dapper hydration can materialize a null payload
	// column, so equality must be null-safe rather than dereferencing directly (guards the NRE in Equals).
	private static bool MessageBodyEquals(byte[]? left, byte[]? right) =>
		left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
