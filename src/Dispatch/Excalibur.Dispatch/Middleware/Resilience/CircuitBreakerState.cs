// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
/// <remarks>
/// State reads by callers may race with mutations. This is by design:
/// requests admitted while the breaker is Closed are allowed to complete
/// even if the state transitions to Open during execution. Internal
/// mutations (RecordSuccess, RecordFailure, TransitionToHalfOpen) are
/// atomic via lock. This matches the behavior of Polly and Resilience4j
/// circuit breakers.
/// </remarks>
internal sealed class CircuitBreakerState(CircuitBreakerOptions options, TimeProvider timeProvider)
{
	private readonly Lock _lock = new();
	private int _failureCount;
	private int _successCount;

	public CircuitState State { get; private set; } = CircuitState.Closed;

	public DateTimeOffset NextAttemptTime { get; private set; } = DateTimeOffset.MinValue;

	public void RecordSuccess()
	{
		lock (_lock)
		{
			_successCount++;
			_failureCount = 0; // Reset failure count on success

			// One success closes the circuit, matching Polly; the configurable threshold was removed.
			if (State == CircuitState.HalfOpen && _successCount >= 1)
			{
				State = CircuitState.Closed;
				_successCount = 0;
			}
		}
	}

	public void RecordFailure()
	{
		lock (_lock)
		{
			_failureCount++;
			_successCount = 0; // Reset success count on failure

			if (State != CircuitState.Open && _failureCount >= options.FailureThreshold)
			{
				State = CircuitState.Open;
				NextAttemptTime = CreateTimestamp().Add(options.OpenDuration);
				_failureCount = 0; // Reset for next cycle
			}
		}
	}

	public void TransitionToHalfOpen()
	{
		lock (_lock)
		{
			if (State == CircuitState.Open)
			{
				State = CircuitState.HalfOpen;
				_successCount = 0;
				_failureCount = 0;
			}
		}
	}

	/// <summary>
	/// Reads the current time from the injected <see cref="TimeProvider"/>.
	/// </summary>
	/// <remarks>
	/// This was <c>DateTimeOffset.UtcNow</c>. The open-duration deadline is the one piece of state that
	/// decides when a half-open probe is allowed, so hard-coding the system clock made the only interesting
	/// transition in this breaker reachable in a test solely by sleeping in real time — which is why the
	/// recovery path had no deterministic coverage. <see cref="TimeProvider"/> is the platform's answer and
	/// lets a fake clock step across the deadline instantly.
	/// </remarks>
	private DateTimeOffset CreateTimestamp() => timeProvider.GetUtcNow();
}
