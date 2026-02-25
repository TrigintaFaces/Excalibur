// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Defines the contract for a circuit breaker policy that protects operations from repeated failures.
/// </summary>
/// <remarks>
/// <para>
/// The circuit breaker pattern prevents cascading failures by "tripping" when a threshold of failures
/// is reached, temporarily rejecting all requests. After a cooldown period, it allows limited traffic
/// through to test if the underlying service has recovered.
/// </para>
/// <para>
/// For diagnostic properties (ConsecutiveFailures, LastOpenedAt) and events (StateChanged),
/// use <c>GetService(typeof(ICircuitBreakerDiagnostics))</c> or <c>GetService(typeof(ICircuitBreakerEvents))</c>.
/// </para>
/// </remarks>
public interface ICircuitBreakerPolicy
{
	/// <summary>
	/// Gets the current state of the circuit breaker.
	/// </summary>
	CircuitState State { get; }

	/// <summary>
	/// Executes an asynchronous operation through the circuit breaker.
	/// </summary>
	/// <typeparam name="TResult">The type of the result.</typeparam>
	/// <param name="action">The action to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the action if successful.</returns>
	/// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open.</exception>
	Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a successful operation, potentially closing the circuit.
	/// </summary>
	void RecordSuccess();

	/// <summary>
	/// Records a failed operation, potentially opening the circuit.
	/// </summary>
	/// <param name="exception">The exception that caused the failure.</param>
	void RecordFailure(Exception? exception = null);

	/// <summary>
	/// Manually resets the circuit breaker to the closed state.
	/// </summary>
	void Reset();
}

/// <summary>
/// Provides diagnostic information about a circuit breaker's operational state.
/// Access via <c>GetService(typeof(ICircuitBreakerDiagnostics))</c> on the policy instance.
/// </summary>
public interface ICircuitBreakerDiagnostics
{
	/// <summary>
	/// Gets the number of consecutive failures since the last success.
	/// </summary>
	int ConsecutiveFailures { get; }

	/// <summary>
	/// Gets the timestamp when the circuit was last opened.
	/// </summary>
	DateTimeOffset? LastOpenedAt { get; }
}

/// <summary>
/// Provides circuit breaker state change events.
/// Access via <c>GetService(typeof(ICircuitBreakerEvents))</c> on the policy instance.
/// </summary>
public interface ICircuitBreakerEvents
{
	/// <summary>
	/// Event raised when the circuit state changes.
	/// </summary>
	event EventHandler<CircuitStateChangedEventArgs>? StateChanged;
}
