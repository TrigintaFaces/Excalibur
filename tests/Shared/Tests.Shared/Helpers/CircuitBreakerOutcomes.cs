// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Tests.Shared.Helpers;

/// <summary>
/// Drives outcomes through a circuit breaker the way production does — via
/// <see cref="ICircuitBreakerPolicy.ExecuteAsync{TResult}(Func{CancellationToken, Task{TResult}}, CancellationToken)"/>.
/// The policy records the outcome itself; there is no out-of-band recorder to call.
/// </summary>
public static class CircuitBreakerOutcomes
{
	/// <summary>
	/// Executes a failing operation through the policy and swallows the resulting exception
	/// (including a rejection when the circuit is already open).
	/// </summary>
	/// <param name="policy">The policy under test.</param>
	/// <param name="exception">The exception the operation throws. Defaults to an <see cref="InvalidOperationException"/>.</param>
	/// <returns>A task that completes once the outcome has been recorded (or rejected).</returns>
	public static async Task FailAsync(this ICircuitBreakerPolicy policy, Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(policy);

		try
		{
			_ = await policy.ExecuteAsync<bool>(
				_ => Task.FromException<bool>(exception ?? new InvalidOperationException("Simulated failure.")),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// The failure is the point; the policy has recorded it (or refused the call).
		}
	}

	/// <summary>
	/// Executes a successful operation through the policy and swallows a rejection when the circuit is open.
	/// </summary>
	/// <param name="policy">The policy under test.</param>
	/// <returns>A task that completes once the outcome has been recorded (or rejected).</returns>
	public static async Task SucceedAsync(this ICircuitBreakerPolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);

		try
		{
			_ = await policy.ExecuteAsync(
				_ => Task.FromResult(true),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException)
		{
			// The circuit refused the call; nothing to record.
		}
	}
	/// <summary>
	/// Waits for the policy to report <paramref name="expected"/>, returning the last observed state.
	/// </summary>
	/// <remarks>
	/// A reset is observed rather than assumed: an implementation backed by an asynchronous manual
	/// control publishes the closed state from its own callback, so a caller that reads
	/// <see cref="ICircuitBreakerPolicy.State"/> on the next line races that callback.
	/// </remarks>
	/// <param name="policy">The policy under test.</param>
	/// <param name="expected">The state to wait for.</param>
	/// <param name="timeout">How long to wait. Defaults to five seconds.</param>
	/// <returns>The expected state once observed, or the last state seen when the wait times out.</returns>
	public static async Task<CircuitState> WaitForStateAsync(
		this ICircuitBreakerPolicy policy,
		CircuitState expected,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(policy);

		var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
		var state = policy.State;

		while (state != expected && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(10).ConfigureAwait(false);
			state = policy.State;
		}

		return state;
	}
}
