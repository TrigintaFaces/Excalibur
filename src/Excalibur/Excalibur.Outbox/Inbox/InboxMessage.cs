// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a message stored in the inbox pattern for reliable message processing.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed record InboxMessage : IInboxMessage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMessage" /> class.
	/// </summary>
	public InboxMessage()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMessage" /> class with required properties.
	/// </summary>
	/// <param name="externalMessageId"> The external identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body bytes. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		DateTimeOffset receivedAt)
	{
		ExternalMessageId = externalMessageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		ReceivedAt = receivedAt;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMessage" /> class with expiration.
	/// </summary>
	/// <param name="externalMessageId"> The external identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body bytes. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	/// <param name="expiresAt"> The optional expiration timestamp for the message. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		DateTimeOffset receivedAt,
		DateTimeOffset? expiresAt)
	{
		ExternalMessageId = externalMessageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		ReceivedAt = receivedAt;
		ExpiresAt = expiresAt;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMessage" /> class with processing tracking.
	/// </summary>
	/// <param name="externalMessageId"> The external identifier for the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The serialized message metadata. </param>
	/// <param name="messageBody"> The serialized message body bytes. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	/// <param name="attempts"> The number of processing attempts. </param>
	/// <param name="dispatcherId"> The optional identifier of the processor handling this message. </param>
	/// <param name="dispatcherTimeout"> The optional timeout for the processor. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		DateTimeOffset receivedAt,
		int attempts,
		string? dispatcherId,
		DateTimeOffset? dispatcherTimeout)
	{
		ExternalMessageId = externalMessageId;
		MessageType = messageType;
		MessageMetadata = messageMetadata;
		MessageBody = messageBody;
		ReceivedAt = receivedAt;
		Attempts = attempts;
		DispatcherId = dispatcherId;
		DispatcherTimeout = dispatcherTimeout;
	}

	/// <summary>
	/// Gets the external identifier for the message from the source system.
	/// </summary>
	/// <value>The current <see cref="ExternalMessageId"/> value.</value>
	public required string ExternalMessageId { get; init; }

	/// <summary>
	/// Gets the name under which the message's .NET type is registered.
	/// </summary>
	/// <value>
	/// A type name the message type registry can resolve. The framework's own inbox writers store the simple
	/// name (<c>Type.Name</c>).
	/// An ambiguous simple name — one shared by two registered types — resolves to
	/// <b>nothing</b> rather than to either of them, so a collision fails loudly at resolution rather
	/// than deserializing the payload as the wrong type.
	/// </value>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets the serialized message metadata.
	/// </summary>
	/// <value>The current <see cref="MessageMetadata"/> value.</value>
	public required string MessageMetadata { get; init; }

	/// <summary>
	/// Gets the serialized message body bytes, exactly as produced by the configured serializer.
	/// </summary>
	/// <value>The current <see cref="MessageBody"/> value.</value>
	public required byte[] MessageBody { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was received.
	/// </summary>
	/// <value>The current <see cref="ReceivedAt"/> value.</value>
	public required DateTimeOffset ReceivedAt { get; init; }

	/// <summary>
	/// Gets or sets the optional expiration timestamp for the message.
	/// </summary>
	/// <value>The current <see cref="ExpiresAt"/> value.</value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts made for this message.
	/// </summary>
	/// <value>The current <see cref="Attempts"/> value.</value>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the optional identifier of the processor handling this message.
	/// </summary>
	/// <value>The current <see cref="DispatcherId"/> value.</value>
	public string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the optional timeout for the processor.
	/// </summary>
	/// <value>The current <see cref="DispatcherTimeout"/> value.</value>
	public DateTimeOffset? DispatcherTimeout { get; set; }

	/// <summary>
	/// Gets the tenant identifier this message was received under, or <see langword="null"/> when no tenant
	/// scope is carried. Populated from the persisted inbox entry so the re-admission drain re-establishes the
	/// entry's tenant scope before dispatch.
	/// </summary>
	/// <value>The current <see cref="TenantId"/> value.</value>
	public string? TenantId { get; init; }

	/// <summary>
	/// Determines whether the specified <see cref="InboxMessage"/> is equal to the current instance,
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
	public bool Equals(InboxMessage? other) =>
		other is not null
		&& ExternalMessageId == other.ExternalMessageId
		&& MessageType == other.MessageType
		&& MessageMetadata == other.MessageMetadata
		&& MessageBodyEquals(MessageBody, other.MessageBody)
		&& ReceivedAt == other.ReceivedAt
		&& ExpiresAt == other.ExpiresAt
		&& Attempts == other.Attempts
		&& DispatcherId == other.DispatcherId
		&& DispatcherTimeout == other.DispatcherTimeout
		&& TenantId == other.TenantId;

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(ExternalMessageId);
		hash.Add(MessageType);
		hash.Add(MessageMetadata);
		if (MessageBody is not null)
		{
			hash.AddBytes(MessageBody);
		}

		hash.Add(ReceivedAt);
		hash.Add(ExpiresAt);
		hash.Add(Attempts);
		hash.Add(DispatcherId);
		hash.Add(DispatcherTimeout);
		hash.Add(TenantId);
		return hash.ToHashCode();
	}

	// MessageBody is declared non-null (required), but store hydration can materialize a null payload
	// column, so equality must be null-safe rather than dereferencing directly (guards the NRE in Equals).
	private static bool MessageBodyEquals(byte[]? left, byte[]? right) =>
		left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
