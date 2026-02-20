// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Defines a circuit breaker for data provider operations, protecting against cascading
/// failures when a downstream data store becomes unavailable or degraded.
/// </summary>
/// <remarks>
/// <para>
/// Reference: <c>Microsoft.Extensions.Resilience</c> / Polly v8 <c>ResiliencePipeline</c> pattern.
/// This interface exposes a minimal surface (2 methods + 1 property) for executing operations
/// through the circuit breaker and querying its state.
/// </para>
/// </remarks>
public interface IDataProviderCircuitBreaker
{
	/// <summary>
	/// Gets the current state of the circuit breaker.
	/// </summary>
	/// <value>The current <see cref="DataProviderCircuitState"/>.</value>
	DataProviderCircuitState State { get; }

	/// <summary>
	/// Executes the specified asynchronous operation through the circuit breaker.
	/// </summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	/// <exception cref="CircuitBreakerOpenException">
	/// Thrown when the circuit breaker is in the <see cref="DataProviderCircuitState.Open"/> state.
	/// </exception>
	Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

	/// <summary>
	/// Manually resets the circuit breaker to the <see cref="DataProviderCircuitState.Closed"/> state.
	/// </summary>
	void Reset();
}
