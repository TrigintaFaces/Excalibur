// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Exception thrown when an operation is rejected because the circuit breaker is in the open state.
/// </summary>
public sealed class CircuitBreakerOpenException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
	/// </summary>
	public CircuitBreakerOpenException()
		: base("The circuit breaker is open. The operation was rejected to prevent cascading failures.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public CircuitBreakerOpenException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public CircuitBreakerOpenException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the time remaining before the circuit breaker transitions to half-open.
	/// </summary>
	/// <value>The remaining break duration, or <see langword="null"/> if unknown.</value>
	public TimeSpan? RetryAfter { get; init; }
}
