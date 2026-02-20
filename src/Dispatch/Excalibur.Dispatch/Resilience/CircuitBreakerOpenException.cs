// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Exception thrown when an operation is rejected because the circuit breaker is open.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	public CircuitBreakerOpenException()
		: base(Resources.CircuitBreakerOpenException_CircuitBreakerIsOpen)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	/// <param name="circuitName"> The name of the circuit breaker. </param>
	public CircuitBreakerOpenException(string circuitName)
		: base(string.Format(
			CultureInfo.CurrentCulture,
			Resources.CircuitBreakerOpenException_CircuitBreakerIsOpenWithName,
			circuitName))
	{
		CircuitName = circuitName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	/// <param name="circuitName"> The name of the circuit breaker. </param>
	/// <param name="retryAfter"> Suggested time to wait before retrying. </param>
	public CircuitBreakerOpenException(string circuitName, TimeSpan retryAfter)
		: base(string.Format(
			CultureInfo.CurrentCulture,
			Resources.CircuitBreakerOpenException_CircuitBreakerIsOpenWithNameAndRetryAfter,
			circuitName,
			retryAfter.TotalSeconds))
	{
		CircuitName = circuitName;
		RetryAfter = retryAfter;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	/// <param name="message"> The error message. </param>
	/// <param name="innerException"> The inner exception. </param>
	public CircuitBreakerOpenException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the name of the circuit breaker that rejected the operation.
	/// </summary>
	public string? CircuitName { get; }

	/// <summary>
	/// Gets the suggested time to wait before retrying, if known.
	/// </summary>
	public TimeSpan? RetryAfter { get; }
}
