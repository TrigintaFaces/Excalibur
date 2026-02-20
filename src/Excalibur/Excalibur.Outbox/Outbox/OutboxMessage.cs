// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

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
	public OutboxMessage(string messageId, string messageType, string messageMetadata, string messageBody, DateTimeOffset createdAt)
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
	public OutboxMessage(
		string messageId,
		string messageType,
		string messageMetadata,
		string messageBody,
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
	public OutboxMessage(string messageId, string messageType, string messageMetadata, string messageBody, DateTimeOffset createdAt,
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
	/// Gets the serialized message body.
	/// </summary>
	/// <value>The current <see cref="MessageBody"/> value.</value>
	public required string MessageBody { get; init; }

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
}
