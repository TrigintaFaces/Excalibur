// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines the identity and payload properties of a scheduled message including
/// its unique identifier, message type, serialized body, and correlation tracking.
/// </summary>
public interface IScheduledMessageIdentity
{
	/// <summary>
	/// Gets or sets the unique identifier for this scheduled message instance.
	/// </summary>
	/// <value> A globally unique identifier that distinguishes this scheduled message from all other scheduled messages in the system. </value>
	Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified type name of the message for deserialization.
	/// </summary>
	/// <value>
	/// The complete type name including namespace that can be used to deserialize the MessageBody back into the appropriate message type instance.
	/// </value>
	string MessageName { get; set; }

	/// <summary>
	/// Gets or sets the serialized message payload that will be dispatched when the schedule executes.
	/// </summary>
	/// <value>
	/// A string containing the serialized message data in the format expected by the configured message serializer (JSON, MessagePack, etc.).
	/// </value>
	string MessageBody { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for linking related messages and operations.
	/// </summary>
	/// <value>
	/// A correlation ID that can be used to group related messages across distributed operations.
	/// </value>
	string? CorrelationId { get; set; }
}
