// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Exceptions;

/// <summary>
/// Thrown when an aggregate's event-application logic does not recognize a domain event type — a missing
/// switch arm in <c>ApplyEventInternal</c>. Enforces totality: an unrecognized event fails loud rather
/// than being silently ignored while the aggregate version still advances (which would rehydrate a state
/// that never legitimately existed).
/// </summary>
public sealed class UnhandledDomainEventException : DomainException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnhandledDomainEventException"/> class.
	/// </summary>
	/// <param name="aggregateType"> The aggregate type whose apply logic did not handle the event. </param>
	/// <param name="eventType"> The unrecognized domain event type. </param>
	public UnhandledDomainEventException(Type aggregateType, Type eventType)
		: base(BuildMessage(aggregateType, eventType))
	{
		AggregateType = aggregateType;
		EventType = eventType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnhandledDomainEventException"/> class with a default message.
	/// </summary>
	public UnhandledDomainEventException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnhandledDomainEventException"/> class with a specified message.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	public UnhandledDomainEventException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnhandledDomainEventException"/> class with a specified
	/// message and inner exception.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public UnhandledDomainEventException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the aggregate type whose apply logic did not handle the event, if known.
	/// </summary>
	/// <value> The aggregate type, or <see langword="null"/> when constructed without one. </value>
	public Type? AggregateType { get; }

	/// <summary>
	/// Gets the unrecognized domain event type, if known.
	/// </summary>
	/// <value> The event type, or <see langword="null"/> when constructed without one. </value>
	public Type? EventType { get; }

	private static string BuildMessage(Type aggregateType, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(aggregateType);
		ArgumentNullException.ThrowIfNull(eventType);

		return $"Aggregate '{aggregateType.Name}' has no handler for event type '{eventType.Name}'. " +
			"Every event applied to an aggregate must be handled by its ApplyEventInternal switch. " +
			"How to fix: add an arm for this event type (or remove the event from the stream). " +
			"An unhandled event is refused rather than silently ignored, because ignoring it would advance " +
			"the aggregate version without applying the state change and rehydrate a corrupt aggregate.";
	}
}
