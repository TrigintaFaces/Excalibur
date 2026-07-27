// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Exceptions;

/// <summary>
/// Thrown when an aggregate's event stream is not contiguous during rehydration — a replayed event's
/// version is not exactly one greater than the previously applied version. Enforces that history is
/// replayed gap-free and in order, rather than inferring the version by counting events (which silently
/// applies the wrong event where a missing one belongs and rehydrates a corrupt aggregate).
/// </summary>
public sealed class EventStreamContiguityException : DomainException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EventStreamContiguityException"/> class.
	/// </summary>
	/// <param name="aggregateType"> The aggregate type being rehydrated. </param>
	/// <param name="expectedVersion"> The version the next event was required to have. </param>
	/// <param name="actualVersion"> The version the next event actually had. </param>
	public EventStreamContiguityException(Type aggregateType, long expectedVersion, long actualVersion)
		: base(BuildMessage(aggregateType, expectedVersion, actualVersion))
	{
		AggregateType = aggregateType;
		ExpectedVersion = expectedVersion;
		ActualVersion = actualVersion;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStreamContiguityException"/> class with a default message.
	/// </summary>
	public EventStreamContiguityException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStreamContiguityException"/> class with a specified message.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	public EventStreamContiguityException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStreamContiguityException"/> class with a specified
	/// message and inner exception.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public EventStreamContiguityException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the aggregate type being rehydrated, if known.
	/// </summary>
	/// <value> The aggregate type, or <see langword="null"/> when constructed without one. </value>
	public Type? AggregateType { get; }

	/// <summary>
	/// Gets the version the next event was required to have.
	/// </summary>
	/// <value> The expected version. </value>
	public long ExpectedVersion { get; }

	/// <summary>
	/// Gets the version the next event actually had.
	/// </summary>
	/// <value> The actual version. </value>
	public long ActualVersion { get; }

	private static string BuildMessage(Type aggregateType, long expectedVersion, long actualVersion)
	{
		ArgumentNullException.ThrowIfNull(aggregateType);

		return $"Aggregate '{aggregateType.Name}' event stream is not contiguous: expected the next event at " +
			$"version {expectedVersion} but found version {actualVersion}. History must be replayed gap-free " +
			"and in order. A gap means events are missing or out of order; applying the available events would " +
			"reconstitute a state that never legitimately existed, so the load is refused.";
	}
}
