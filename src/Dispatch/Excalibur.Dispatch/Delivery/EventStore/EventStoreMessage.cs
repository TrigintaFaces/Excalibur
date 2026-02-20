// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a message stored in an event store.
/// </summary>
/// <typeparam name="TAggregateKey"> The type of the aggregate identifier. </typeparam>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class EventStoreMessage<TAggregateKey> : IEventStoreMessage<TAggregateKey>
	where TAggregateKey : notnull
{
	/// <summary>
	/// Gets the aggregate identifier.
	/// </summary>
	/// <value>The current <see cref="AggregateId"/> value.</value>
	public required TAggregateKey AggregateId { get; init; }

	/// <summary>
	/// Gets the timestamp when the event occurred.
	/// </summary>
	/// <value>The current <see cref="OccurredOn"/> value.</value>
	public required DateTimeOffset OccurredOn { get; init; }

	/// <summary>
	/// Gets the unique identifier for the event.
	/// </summary>
	/// <value>The current <see cref="EventId"/> value.</value>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the type of the event.
	/// </summary>
	/// <value>The current <see cref="EventType"/> value.</value>
	public required string EventType { get; init; }

	/// <summary>
	/// Gets the serialized event body.
	/// </summary>
	/// <value>The current <see cref="EventBody"/> value.</value>
	public required string EventBody { get; init; }

	/// <summary>
	/// Gets the event metadata.
	/// </summary>
	/// <value>The current <see cref="EventMetadata"/> value.</value>
	public required string EventMetadata { get; init; }

	/// <summary>
	/// Gets or sets the number of dispatch attempts.
	/// </summary>
	/// <value>The current <see cref="Attempts"/> value.</value>
	public int Attempts { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the dispatcher handling this message.
	/// </summary>
	/// <value>The current <see cref="DispatcherId"/> value.</value>
	public string? DispatcherId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was dispatched.
	/// </summary>
	/// <value>The current <see cref="DispatchedOn"/> value.</value>
	public DateTimeOffset? DispatchedOn { get; set; }

	/// <summary>
	/// Gets or sets the timeout for the dispatcher.
	/// </summary>
	/// <value>The current <see cref="DispatcherTimeout"/> value.</value>
	public DateTimeOffset? DispatcherTimeout { get; set; }

	/// <summary>
	/// Converts an EventStoreMessage with string key to one with the specified aggregate key type.
	/// </summary>
	/// <param name="source"> The source message to convert. </param>
	/// <returns> A new EventStoreMessage with the specified key type. </returns>
	/// <exception cref="NotSupportedException"> Thrown when conversion is not implemented. </exception>
	public static EventStoreMessage<TAggregateKey> FromEventStoreMessage(EventStoreMessage<string> source) => throw

		// Proper implementation would go here For now, just throwing NotSupportedException with a descriptive message
		new NotSupportedException(Resources.EventStoreMessage_ConversionFromStringToAggregateKeyNotImplemented);
}
