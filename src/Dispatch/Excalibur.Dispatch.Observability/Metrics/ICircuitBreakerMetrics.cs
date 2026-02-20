// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Defines the contract for Circuit Breaker metrics collection.
/// </summary>
public interface ICircuitBreakerMetrics
{
	/// <summary>
	/// Gets the meter instance.
	/// </summary>
	Meter Meter { get; }

	/// <summary>
	/// Records a circuit breaker state change.
	/// </summary>
	/// <param name="circuitName">The name of the circuit breaker.</param>
	/// <param name="previousState">The previous state (Closed, Open, HalfOpen).</param>
	/// <param name="newState">The new state (Closed, Open, HalfOpen).</param>
	void RecordStateChange(string circuitName, string previousState, string newState);

	/// <summary>
	/// Records a rejection when the circuit breaker is open.
	/// </summary>
	/// <param name="circuitName">The name of the circuit breaker.</param>
	void RecordRejection(string circuitName);

	/// <summary>
	/// Updates the current state gauge for a circuit breaker.
	/// </summary>
	/// <param name="circuitName">The name of the circuit breaker.</param>
	/// <param name="state">The current state value (0=Closed, 1=Open, 2=HalfOpen).</param>
	void UpdateState(string circuitName, int state);

	/// <summary>
	/// Records a failure that contributed to the circuit breaker state.
	/// </summary>
	/// <param name="circuitName">The name of the circuit breaker.</param>
	/// <param name="exceptionType">The type of exception that occurred.</param>
	void RecordFailure(string circuitName, string exceptionType);

	/// <summary>
	/// Records a successful operation through the circuit breaker.
	/// </summary>
	/// <param name="circuitName">The name of the circuit breaker.</param>
	void RecordSuccess(string circuitName);
}
