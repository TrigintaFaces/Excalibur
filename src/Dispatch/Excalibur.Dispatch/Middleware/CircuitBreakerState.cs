// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
internal sealed class CircuitBreakerState(CircuitBreakerOptions options)
{
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
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

			if (State == CircuitState.HalfOpen && _successCount >= options.SuccessThreshold)
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

	private static DateTimeOffset CreateTimestamp() => DateTimeOffset.UtcNow;
}
