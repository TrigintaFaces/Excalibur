// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines the contract for a message stored in an event store.
/// </summary>
public interface IEventStoreMessage
{
	/// <summary>
	/// Gets the timestamp when the event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the event occurred.
	/// </value>
	DateTimeOffset OccurredOn { get; init; }

	/// <summary>
	/// Gets the unique identifier for the event.
	/// </summary>
	/// <value>
	/// The unique identifier for the event.
	/// </value>
	string EventId { get; init; }

	/// <summary>
	/// Gets the type of the event.
	/// </summary>
	/// <value>
	/// The type of the event.
	/// </value>
	string EventType { get; init; }

	/// <summary>
	/// Gets the serialized event body.
	/// </summary>
	/// <value>
	/// The serialized event body.
	/// </value>
	string EventBody { get; init; }

	/// <summary>
	/// Gets the event metadata.
	/// </summary>
	/// <value>
	/// The event metadata.
	/// </value>
	string EventMetadata { get; init; }

	/// <summary>
	/// Gets or sets the number of dispatch attempts.
	/// </summary>
	/// <value>
	/// The number of dispatch attempts.
	/// </value>
	int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the dispatcher handling this message.
	/// </summary>
	/// <value>
	/// The identifier of the dispatcher handling this message.
	/// </value>
	string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was dispatched.
	/// </summary>
	/// <value>
	/// The timestamp when the message was dispatched.
	/// </value>
	DateTimeOffset? DispatchedOn { get; set; }

	/// <summary>
	/// Gets or sets the timeout for the dispatcher.
	/// </summary>
	/// <value>
	/// The timeout for the dispatcher.
	/// </value>
	DateTimeOffset? DispatcherTimeout { get; set; }
}

/// <summary>
/// Defines the contract for a message stored in an event store with a specific aggregate key type.
/// </summary>
/// <typeparam name="TAggregateKey"> The type of the aggregate identifier. </typeparam>
public interface IEventStoreMessage<out TAggregateKey> : IEventStoreMessage
{
	/// <summary>
	/// Gets the aggregate identifier.
	/// </summary>
	/// <value>
	/// The aggregate identifier.
	/// </value>
	TAggregateKey AggregateId { get; }
}
