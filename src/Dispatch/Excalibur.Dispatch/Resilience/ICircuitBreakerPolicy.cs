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
/// <b>A policy's configuration is fixed for its lifetime.</b> The values an implementation is created
/// with are the values it uses until it is discarded: no member of this interface changes them, and
/// neither does any later change to the object they were read from. Configuration types in this
/// framework are settable so they can take part in the options pattern -- binding, <c>Configure</c>
/// callbacks and startup validation all require setters -- so a caller may still hold the instance it
/// passed and alter it afterwards. Doing so has no effect on a policy already built from it, on any
/// implementation. To run with different settings, create a new policy; do not mutate and expect the
/// existing one to follow.
/// </para>
/// <para>
/// Diagnostic properties (ConsecutiveFailures, LastOpenedAt) and events (StateChanged) are kept off
/// this interface so an implementation is not obliged to carry them. Test the policy instance for
/// <see cref="ICircuitBreakerDiagnostics"/> or <see cref="ICircuitBreakerEvents"/> to reach them.
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
	/// <remarks>
	/// <b>This method records the outcome itself.</b> An implementation registers the success, or registers
	/// the failure and rethrows, before control returns to the caller. Recording an outcome is not something
	/// a caller can do -- or need to do -- separately.
	/// </remarks>
	/// <typeparam name="TResult">The type of the result.</typeparam>
	/// <param name="action">The action to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the action if successful.</returns>
	/// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open.</exception>
	Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an action through the circuit, treating a returned value as a failure when
	/// <paramref name="isFailure"/> says so.
	/// </summary>
	/// <typeparam name="TResult">The result type.</typeparam>
	/// <param name="action">The action to execute.</param>
	/// <param name="isFailure">
	/// Decides whether a returned result counts as a failure. Not every failure is an exception: a
	/// pipeline that returns a failed result would otherwise look healthy to the circuit forever.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the action.</returns>
	/// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is open.</exception>
	Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		Func<TResult, bool> isFailure,
		CancellationToken cancellationToken);

	/// <summary>
	/// Manually returns the circuit breaker to the closed state.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes once the circuit is closed.</returns>
	/// <remarks>
	/// <b>When the returned task completes, <see cref="State" /> is <see cref="CircuitState.Closed" />.</b>
	/// That is the postcondition an operator depends on: the point of a manual reset is to put a circuit
	/// back into service, and a caller that cannot tell when that has happened has to poll or guess.
	/// An implementation whose close is asynchronous must therefore await it and publish the closed state
	/// before completing, and a close that fails must surface as a faulted task rather than a log line.
	/// </remarks>
	Task ResetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Provides diagnostic information about a circuit breaker's operational state.
/// Test an <see cref="ICircuitBreakerPolicy"/> instance for this interface to reach it.
/// </summary>
public interface ICircuitBreakerDiagnostics
{
	// A policy that measures nothing must not implement this interface. Every member here is a
	// measurement, so a pass-through implementation could only answer with fabricated constants, and a
	// caller reading them could not tell "no breaker installed" from "breaker installed and healthy".

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
/// Test an <see cref="ICircuitBreakerPolicy"/> instance for this interface to reach it.
/// </summary>
public interface ICircuitBreakerEvents
{
	/// <summary>
	/// Event raised when the circuit state changes.
	/// </summary>
	event EventHandler<CircuitStateChangedEventArgs>? StateChanged;
}
