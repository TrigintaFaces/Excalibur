// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

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
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		string messageBody,
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
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	/// <param name="expiresAt"> The optional expiration timestamp for the message. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		string messageBody,
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
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="receivedAt"> The timestamp when the message was received. </param>
	/// <param name="attempts"> The number of processing attempts. </param>
	/// <param name="dispatcherId"> The optional identifier of the processor handling this message. </param>
	/// <param name="dispatcherTimeout"> The optional timeout for the processor. </param>
	[SetsRequiredMembers]
	public InboxMessage(
		string externalMessageId,
		string messageType,
		string messageMetadata,
		string messageBody,
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
}
