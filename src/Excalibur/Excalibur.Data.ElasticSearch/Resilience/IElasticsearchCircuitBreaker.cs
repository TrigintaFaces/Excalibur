// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Provides circuit breaker functionality to prevent cascading failures in Elasticsearch operations.
/// </summary>
public interface IElasticsearchCircuitBreaker : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the circuit breaker is currently in the open state.
	/// </summary>
	/// <value> True if the circuit is open (blocking requests), false otherwise. </value>
	bool IsOpen { get; }

	/// <summary>
	/// Gets a value indicating whether the circuit breaker is currently in the half-open state.
	/// </summary>
	/// <value> True if the circuit is half-open (allowing test requests), false otherwise. </value>
	bool IsHalfOpen { get; }

	/// <summary>
	/// Gets the current state of the circuit breaker.
	/// </summary>
	/// <value> The current circuit breaker state. </value>
	CircuitBreakerState State { get; }

	/// <summary>
	/// Gets the current failure rate as a percentage (0.0 to 1.0).
	/// </summary>
	/// <value> The current failure rate. </value>
	double FailureRate { get; }

	/// <summary>
	/// Gets the number of consecutive failures recorded.
	/// </summary>
	/// <value> The consecutive failure count. </value>
	int ConsecutiveFailures { get; }

	/// <summary>
	/// Records a successful operation, which may contribute to closing an open circuit.
	/// </summary>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RecordSuccessAsync();

	/// <summary>
	/// Records a failed operation, which may contribute to opening the circuit.
	/// </summary>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RecordFailureAsync();

	/// <summary>
	/// Attempts to execute an operation through the circuit breaker.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the operation if the circuit allows execution. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the circuit is open and the operation is blocked. </exception>
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
}
