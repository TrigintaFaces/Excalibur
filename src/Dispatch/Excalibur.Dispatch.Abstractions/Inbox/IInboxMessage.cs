// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a message stored in the inbox for idempotent processing and guaranteed delivery. The inbox pattern ensures that each message
/// is processed exactly once, even in the face of failures, network issues, or application restarts.
/// </summary>
public interface IInboxMessage
{
	/// <summary>
	/// Gets the unique identifier for this message from the external source. This ID is provided by the messaging system (e.g., SQS message
	/// ID, Service Bus message ID) and is used for correlation and deduplication purposes.
	/// </summary>
	/// <value> A string representing the external message identifier that must be unique within the message source. </value>
	string ExternalMessageId { get; init; }

	/// <summary>
	/// Gets the fully qualified type name of the message payload. This is used for message deserialization and routing to appropriate handlers.
	/// </summary>
	/// <value> A string containing the .NET type name, typically in the format "Namespace.TypeName, AssemblyName". </value>
	string MessageType { get; init; }

	/// <summary>
	/// Gets the serialized metadata associated with this message. Metadata includes headers, correlation IDs, tracing information, and
	/// other message properties that are separate from the actual message payload.
	/// </summary>
	/// <value> A JSON string containing the message metadata, or an empty string if no metadata exists. </value>
	string MessageMetadata { get; init; }

	/// <summary>
	/// Gets the serialized message payload body. This contains the actual message data that will be deserialized and processed by message handlers.
	/// </summary>
	/// <value> A string containing the serialized message payload, typically in JSON format. </value>
	string MessageBody { get; init; }

	/// <summary>
	/// Gets the timestamp when this message was first received and stored in the inbox. This is used for ordering, debugging, and message
	/// lifecycle management.
	/// </summary>
	/// <value> A <see cref="DateTimeOffset" /> representing when the message was received, in UTC. </value>
	DateTimeOffset ReceivedAt { get; init; }

	/// <summary>
	/// Gets or sets the expiration time for this message. Messages that expire will be automatically cleaned up and will not be processed.
	/// A null value indicates the message never expires.
	/// </summary>
	/// <value> A <see cref="DateTimeOffset" /> representing when the message expires, or null for no expiration. </value>
	DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts made for this message. This is incremented each time message processing fails and is
	/// used for retry logic and dead letter queue decisions.
	/// </summary>
	/// <value> An integer representing the number of processing attempts, starting from 0. </value>
	int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the dispatcher currently processing this message. This is used to prevent multiple dispatchers from
	/// processing the same message concurrently and to track which dispatcher is responsible for processing.
	/// </summary>
	/// <value> A string identifier for the dispatcher, or null if no dispatcher is currently processing this message. </value>
	string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the timeout timestamp for the current dispatcher processing this message. If the dispatcher does not complete
	/// processing before this timeout, the message becomes available for processing by other dispatchers.
	/// </summary>
	/// <value>
	/// A <see cref="DateTimeOffset" /> representing when the current dispatcher's lease expires, or null if no dispatcher is processing.
	/// </value>
	DateTimeOffset? DispatcherTimeout { get; set; }
}
