// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Exception thrown when a circuit breaker is open and requests are rejected.
/// </summary>
/// <remarks>
/// This is a temporary copy in Core to avoid circular dependencies. This will be refactored to a dedicated Patterns project when
/// CloudNative patterns are reorganized.
/// </remarks>
public sealed class CircuitBreakerOpenException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	public CircuitBreakerOpenException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	/// <param name="message"> The error message. </param>
	public CircuitBreakerOpenException(string message)
		: base(message)
	{
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
	/// Gets or sets the circuit breaker key.
	/// </summary>
	/// <value>The current <see cref="CircuitBreakerKey"/> value.</value>
	public string? CircuitBreakerKey { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker name.
	/// </summary>
	/// <value>The current <see cref="CircuitBreakerName"/> value.</value>
	public string? CircuitBreakerName { get; set; }

	/// <summary>
	/// Gets or sets the time after which to retry.
	/// </summary>
	/// <value>The current <see cref="RetryAfter"/> value.</value>
	public TimeSpan? RetryAfter { get; set; }

	/// <summary>
	/// Gets or sets the failure count that triggered opening.
	/// </summary>
	/// <value>The current <see cref="FailureCount"/> value.</value>
	public int FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the current circuit state.
	/// </summary>
	/// <value>The current <see cref="State"/> value.</value>
	public CircuitState State { get; set; }
}
