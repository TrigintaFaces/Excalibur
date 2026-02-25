// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// A no-op implementation of <see cref="ICircuitBreakerPolicy"/> that passes all operations through.
/// </summary>
/// <remarks>
/// This implementation follows the Null Object pattern to provide a safe default when
/// circuit breaker functionality is not configured or not needed. The circuit is always
/// closed and all operations pass through without protection.
/// </remarks>
public sealed class NullCircuitBreakerPolicy : ICircuitBreakerPolicy, ICircuitBreakerDiagnostics, ICircuitBreakerEvents
{
	private NullCircuitBreakerPolicy()
	{
	}

	/// <summary>
	/// Event raised when the circuit state changes. No-op in null implementation.
	/// </summary>
	public event EventHandler<CircuitStateChangedEventArgs>? StateChanged
	{
		add { }
		remove { }
	}

	/// <summary>
	/// Gets the singleton instance of the null circuit breaker policy.
	/// </summary>
	public static NullCircuitBreakerPolicy Instance { get; } = new();

	/// <inheritdoc />
	public CircuitState State => CircuitState.Closed;

	/// <summary>
	/// Gets the number of consecutive failures. Always returns 0.
	/// </summary>
	public int ConsecutiveFailures => 0;

	/// <summary>
	/// Gets the timestamp when the circuit was last opened. Always returns null.
	/// </summary>
	public DateTimeOffset? LastOpenedAt => null;

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		return await action(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void RecordSuccess()
	{
		// No-op
	}

	/// <inheritdoc />
	public void RecordFailure(Exception? exception = null)
	{
		// No-op
	}

	/// <inheritdoc />
	public void Reset()
	{
		// No-op
	}
}
