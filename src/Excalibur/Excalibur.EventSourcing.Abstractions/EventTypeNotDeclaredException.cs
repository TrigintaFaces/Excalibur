// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.EventSourcing;

/// <summary>
/// The exception thrown when an event's type is not declared by the type-info resolver the host
/// configured, so the store cannot serialize it.
/// </summary>
/// <remarks>
/// <para>
/// A host that supplies a source-generated type-info resolver is stating the closed set of event types
/// its process will ever write. An event outside that set cannot be serialized, and no retry, reload or
/// reconfiguration at run time changes that — only adding the type to the resolver does. It is therefore
/// a defect in the calling program, not an outcome of the append, and is surfaced as a thrown exception
/// rather than a returned <see cref="AppendResult"/> failure. The same rule places
/// <see cref="EventBatchTooLargeException"/> and a blank aggregate identifier on the throwing side.
/// </para>
/// <para>
/// The distinction is load-bearing. A returned failure is indistinguishable from a transient store fault,
/// so a caller retries it; here every retry fails identically and the events are never persisted. Failing
/// loudly on the first append surfaces the missing declaration at once, which is when it can still be
/// fixed.
/// </para>
/// <para>
/// It derives from <see cref="NotSupportedException"/>, which is what the underlying serializer raises for
/// an undeclared type and what the exception carries as its <see cref="Exception.InnerException"/>. A
/// caller already handling that broader type keeps working; a caller wanting the offending type reads
/// <see cref="EventType"/> rather than parsing a message.
/// </para>
/// </remarks>
public sealed class EventTypeNotDeclaredException : NotSupportedException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EventTypeNotDeclaredException"/> class describing the
	/// event the configured resolver does not declare.
	/// </summary>
	/// <param name="eventType">The CLR type of the event the resolver does not declare.</param>
	/// <param name="aggregateId">The identifier of the stream the append targeted.</param>
	/// <param name="aggregateType">The aggregate type name the append targeted.</param>
	/// <param name="innerException">The serializer exception that reported the missing metadata.</param>
	public EventTypeNotDeclaredException(
		Type? eventType,
		string? aggregateId,
		string? aggregateType,
		Exception? innerException)
		: base(BuildMessage(eventType, aggregateId, aggregateType), innerException)
	{
		EventType = eventType;
		AggregateId = aggregateId;
		AggregateType = aggregateType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventTypeNotDeclaredException"/> class.
	/// </summary>
	public EventTypeNotDeclaredException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventTypeNotDeclaredException"/> class with a message.
	/// </summary>
	/// <param name="message">A message naming the undeclared type and how to declare it.</param>
	public EventTypeNotDeclaredException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventTypeNotDeclaredException"/> class with a message
	/// and an inner exception.
	/// </summary>
	/// <param name="message">A message naming the undeclared type and how to declare it.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public EventTypeNotDeclaredException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the CLR type of the event the configured resolver does not declare.
	/// </summary>
	/// <value>The undeclared event type, or <see langword="null"/> when not specified.</value>
	public Type? EventType { get; }

	/// <summary>
	/// Gets the identifier of the stream the refused append targeted.
	/// </summary>
	/// <value>The aggregate identifier, or <see langword="null"/> when not specified.</value>
	public string? AggregateId { get; }

	/// <summary>
	/// Gets the aggregate type name the refused append targeted.
	/// </summary>
	/// <value>The aggregate type name, or <see langword="null"/> when not specified.</value>
	public string? AggregateType { get; }

	private static string BuildMessage(Type? eventType, string? aggregateId, string? aggregateType) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Event type '{0}' is not declared by the configured event type-info resolver, so it cannot be "
			+ "serialized and no event was appended to stream '{1}' of aggregate type '{2}'. Declare the "
			+ "type on the resolver's context (for example with JsonSerializableAttribute) and append again; "
			+ "retrying without that change fails identically.",
			eventType?.FullName ?? "<unknown>",
			aggregateId ?? "<unknown>",
			aggregateType ?? "<unknown>");
}
