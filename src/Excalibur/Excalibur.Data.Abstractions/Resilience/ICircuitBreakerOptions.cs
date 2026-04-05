// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Configuration options for circuit breaker policies.
/// </summary>
public interface ICircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets the circuit breaker failure threshold before opening the circuit.
	/// </summary>
	/// <value>
	/// The circuit breaker failure threshold before opening the circuit.
	/// </value>
	int CircuitBreakerFailureThreshold { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker sampling duration for tracking failures.
	/// </summary>
	/// <value>
	/// The circuit breaker sampling duration for tracking failures.
	/// </value>
	TimeSpan CircuitBreakerSamplingDuration { get; set; }

	/// <summary>
	/// Gets or sets the minimum throughput required for circuit breaker evaluation.
	/// </summary>
	/// <value>
	/// The minimum throughput required for circuit breaker evaluation.
	/// </value>
	int CircuitBreakerMinimumThroughput { get; set; }

	/// <summary>
	/// Gets or sets the duration the circuit breaker stays open before attempting to close.
	/// </summary>
	/// <value>
	/// The duration the circuit breaker stays open before attempting to close.
	/// </value>
	TimeSpan CircuitBreakerDurationOfBreak { get; set; }
}
