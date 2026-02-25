// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

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
	/// Gets the serialized body content of the message.
	/// </summary>
	/// <value>
	/// The serialized body content of the message.
	/// </value>
	string MessageBody { get; init; }

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
}
