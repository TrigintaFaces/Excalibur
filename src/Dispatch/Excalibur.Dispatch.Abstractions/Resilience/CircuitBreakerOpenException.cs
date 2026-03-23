// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Exception thrown when an operation is rejected because the circuit breaker is open.
/// </summary>
/// <remarks>
/// This is the canonical circuit breaker exception used across both Dispatch and Excalibur layers.
/// It inherits from <see cref="Abstractions.ApiException"/> because a circuit-breaker rejection
/// represents an operational error within the Dispatch framework.
/// </remarks>
public sealed class CircuitBreakerOpenException : Abstractions.ApiException
{
	private const string DefaultMessage = "The circuit breaker is open. The operation was rejected to prevent cascading failures.";
	private static readonly CompositeFormat NamedMessageFormat = CompositeFormat.Parse("Circuit breaker '{0}' is open.");
	private static readonly CompositeFormat NamedRetryMessageFormat = CompositeFormat.Parse("Circuit breaker '{0}' is open. Retry after {1:F1}s.");

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	public CircuitBreakerOpenException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerOpenException" /> class.
	/// </summary>
	/// <param name="circuitName"> The name of the circuit breaker. </param>
	public CircuitBreakerOpenException(string circuitName)
		: base(string.Format(CultureInfo.InvariantCulture, NamedMessageFormat, circuitName))
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
			CultureInfo.InvariantCulture,
			NamedRetryMessageFormat,
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
	/// Gets or initializes the suggested time to wait before retrying, if known.
	/// </summary>
	/// <value>The remaining break duration, or <see langword="null"/> if unknown.</value>
	public TimeSpan? RetryAfter { get; init; }
}
