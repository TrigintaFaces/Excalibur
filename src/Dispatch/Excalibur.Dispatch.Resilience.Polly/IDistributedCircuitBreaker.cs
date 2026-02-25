// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Distributed circuit breaker for coordinating state across multiple instances.
/// </summary>
public interface IDistributedCircuitBreaker
{
	/// <summary>
	/// Gets the current state of the circuit breaker.
	/// </summary>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the distributed circuit state.</returns>
	Task<CircuitState> GetStateAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Executes an operation with distributed circuit breaker protection.
	/// </summary>
	/// <typeparam name="T">The type returned by the asynchronous <paramref name="operation"/>.</typeparam>
	/// <param name="operation">The operation to execute within the circuit breaker.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the asynchronous result.</returns>
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

	/// <summary>
	/// Records a success in the distributed state.
	/// </summary>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the update is persisted.</returns>
	Task RecordSuccessAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Records a failure in the distributed state.
	/// </summary>
	/// <param name="exception">The exception being evaluated.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the update is persisted.</returns>
	Task RecordFailureAsync(CancellationToken cancellationToken, Exception? exception = null);

	/// <summary>
	/// Resets the circuit breaker across all instances.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the reset operation.</returns>
	Task ResetAsync(CancellationToken cancellationToken);
}
