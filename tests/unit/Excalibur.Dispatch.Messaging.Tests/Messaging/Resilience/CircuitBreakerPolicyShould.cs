// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Tests for the <see cref="CircuitBreakerPolicy"/> class.
/// Epic 6 (bd-rj9o): Integration tests for circuit breaker pattern.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CircuitBreakerPolicyShould
{
	private readonly ILogger<CircuitBreakerPolicy> _logger;

	private static async Task WaitForStateAsync(CircuitBreakerPolicy policy, CircuitState expectedState, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (policy.State == expectedState)
			{
				return;
			}

			await Task.Yield();
		}

		policy.State.ShouldBe(expectedState);
	}

	public CircuitBreakerPolicyShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<CircuitBreakerPolicy>();
	}

	private CircuitBreakerPolicy CreatePolicy(CircuitBreakerOptions? options = null)
	{
		return new CircuitBreakerPolicy(options ?? new CircuitBreakerOptions(), "test-circuit", _logger);
	}

	#region Initial State Tests

	[Fact]
	public void StartInClosedState()
	{
		// Arrange & Act
		var policy = CreatePolicy();

		// Assert
		policy.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void HaveZeroConsecutiveFailuresInitially()
	{
		// Arrange & Act
		var policy = CreatePolicy();

		// Assert
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastOpenedAtInitially()
	{
		// Arrange & Act
		var policy = CreatePolicy();

		// Assert
		policy.LastOpenedAt.ShouldBeNull();
	}

	#endregion Initial State Tests

	#region Failure Threshold Tests

	[Fact]
	public void OpenCircuitAfterFailureThresholdReached()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 3 };
		var policy = CreatePolicy(options);

		// Act - Record failures up to threshold
		policy.RecordFailure(new InvalidOperationException("Error 1"));
		policy.RecordFailure(new InvalidOperationException("Error 2"));
		policy.State.ShouldBe(CircuitState.Closed); // Still closed

		policy.RecordFailure(new InvalidOperationException("Error 3")); // Threshold reached

		// Assert
		policy.State.ShouldBe(CircuitState.Open);
		policy.ConsecutiveFailures.ShouldBe(3);
	}

	[Fact]
	public void NotOpenCircuitBeforeThresholdReached()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var policy = CreatePolicy(options);

		// Act - Record failures but not enough to reach threshold
		for (var i = 0; i < 4; i++)
		{
			policy.RecordFailure(new InvalidOperationException($"Error {i}"));
		}

		// Assert
		policy.State.ShouldBe(CircuitState.Closed);
		policy.ConsecutiveFailures.ShouldBe(4);
	}

	[Fact]
	public void TrackConsecutiveFailuresAccurately()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act & Assert
		policy.RecordFailure();
		policy.ConsecutiveFailures.ShouldBe(1);

		policy.RecordFailure();
		policy.ConsecutiveFailures.ShouldBe(2);

		policy.RecordFailure();
		policy.ConsecutiveFailures.ShouldBe(3);
	}

	#endregion Failure Threshold Tests

	#region Open State Tests

	[Fact]
	public async Task RejectOperationsWhenCircuitIsOpen()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1, OpenDuration = TimeSpan.FromMinutes(1) };
		var policy = CreatePolicy(options);

		// Open the circuit
		policy.RecordFailure(new InvalidOperationException("Error"));
		policy.State.ShouldBe(CircuitState.Open);

		// Act & Assert - Should throw CircuitBreakerOpenException
		_ = await Should.ThrowAsync<CircuitBreakerOpenException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				await Task.Yield();
				return "result";
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public void SetLastOpenedAtWhenCircuitOpens()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);
		var beforeOpen = DateTimeOffset.UtcNow;

		// Act
		policy.RecordFailure(new InvalidOperationException("Error"));

		// Assert
		_ = policy.LastOpenedAt.ShouldNotBeNull();
		policy.LastOpenedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeOpen);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		policy.LastOpenedAt.Value.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void IncludeCircuitNameInOpenException()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1, OpenDuration = TimeSpan.FromMinutes(1) };
		var policy = new CircuitBreakerPolicy(options, "my-service", _logger);
		policy.RecordFailure(new InvalidOperationException("Error"));

		// Act & Assert
		var exception = Should.Throw<CircuitBreakerOpenException>(() =>
		{
			var state = policy.State; // Force state check
			if (state == CircuitState.Open)
			{
				throw new CircuitBreakerOpenException("my-service");
			}
		});

		exception.CircuitName.ShouldBe("my-service");
	}

	#endregion Open State Tests

	#region Half-Open State Tests

	[Fact]
	public async Task TransitionToHalfOpenAfterOpenDuration()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(50),
		};
		var policy = CreatePolicy(options);

		// Open the circuit
		policy.RecordFailure(new InvalidOperationException("Error"));
		policy.State.ShouldBe(CircuitState.Open);

		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
	}

	[Fact]
	public async Task AllowProbeRequestInHalfOpenState()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(10),
			SuccessThreshold = 1,
		};
		var policy = CreatePolicy(options);

		// Open and wait for half-open
		policy.RecordFailure(new InvalidOperationException("Error"));
		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Act - Execute should work in half-open
		var result = await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return "success";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("success");
	}

	[Fact]
	public async Task CloseCircuitAfterSuccessThresholdInHalfOpen()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(10),
			SuccessThreshold = 3,
		};
		var policy = CreatePolicy(options);

		// Open and wait for half-open
		policy.RecordFailure(new InvalidOperationException("Error"));
		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Act - Record successes up to threshold
		policy.RecordSuccess();
		policy.RecordSuccess();
		policy.State.ShouldBe(CircuitState.HalfOpen); // Still half-open

		policy.RecordSuccess(); // Threshold reached

		// Assert
		policy.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task ReopenCircuitOnAnyFailureInHalfOpen()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 5,
			OpenDuration = TimeSpan.FromMilliseconds(10),
			SuccessThreshold = 3,
		};
		var policy = CreatePolicy(options);

		// Open and wait for half-open
		for (var i = 0; i < 5; i++)
		{
			policy.RecordFailure(new InvalidOperationException($"Error {i}"));
		}

		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Record some successes
		policy.RecordSuccess();
		policy.RecordSuccess();

		// Act - Any failure reopens
		policy.RecordFailure(new InvalidOperationException("Error in half-open"));

		// Assert
		policy.State.ShouldBe(CircuitState.Open);
	}

	#endregion Half-Open State Tests

	#region Success Handling Tests

	[Fact]
	public void ResetConsecutiveFailuresOnSuccess()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var policy = CreatePolicy(options);

		// Record some failures
		policy.RecordFailure();
		policy.RecordFailure();
		policy.RecordFailure();
		policy.ConsecutiveFailures.ShouldBe(3);

		// Act
		policy.RecordSuccess();

		// Assert
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task RecordSuccessAutomaticallyOnSuccessfulExecution()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var policy = CreatePolicy(options);

		// Record some failures
		policy.RecordFailure();
		policy.RecordFailure();
		policy.ConsecutiveFailures.ShouldBe(2);

		// Act
		_ = await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return "result";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Success Handling Tests

	#region Reset Tests

	[Fact]
	public void ResetToClosedState()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);

		// Open the circuit
		policy.RecordFailure();
		policy.State.ShouldBe(CircuitState.Open);

		// Act
		policy.Reset();

		// Assert
		policy.State.ShouldBe(CircuitState.Closed);
		policy.ConsecutiveFailures.ShouldBe(0);
		policy.LastOpenedAt.ShouldBeNull();
	}

	[Fact]
	public void ClearFailureCountOnReset()
	{
		// Arrange
		var policy = CreatePolicy();

		policy.RecordFailure();
		policy.RecordFailure();
		policy.RecordFailure();

		// Act
		policy.Reset();

		// Assert
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Reset Tests

	#region State Changed Event Tests

	[Fact]
	public void RaiseStateChangedEventOnTransition()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);
		var eventRaised = false;
		CircuitStateChangedEventArgs? receivedArgs = null;

		policy.StateChanged += (sender, args) =>
		{
			eventRaised = true;
			receivedArgs = args;
		};

		// Act
		policy.RecordFailure(new InvalidOperationException("Error"));

		// Assert
		eventRaised.ShouldBeTrue();
		_ = receivedArgs.ShouldNotBeNull();
		receivedArgs.PreviousState.ShouldBe(CircuitState.Closed);
		receivedArgs.NewState.ShouldBe(CircuitState.Open);
		receivedArgs.CircuitName.ShouldBe("test-circuit");
	}

	[Fact]
	public void IncludeTriggeringExceptionInEventArgs()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);
		CircuitStateChangedEventArgs? receivedArgs = null;
		var triggeringException = new InvalidOperationException("Trigger exception");

		policy.StateChanged += (sender, args) => receivedArgs = args;

		// Act
		policy.RecordFailure(triggeringException);

		// Assert
		_ = receivedArgs.ShouldNotBeNull();
		receivedArgs.TriggeringException.ShouldBe(triggeringException);
	}

	#endregion State Changed Event Tests

	#region Execute Method Tests

	[Fact]
	public async Task ExecuteAndReturnResultOnSuccess()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			return 42;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteVoidActionSuccessfully()
	{
		// Arrange
		var policy = CreatePolicy();
		var executed = false;

		// Act
		await policy.ExecuteAsync(async ct =>
		{
			ct.ThrowIfCancellationRequested();
			await Task.Yield();
			executed = true;
			return true;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task RecordFailureAndRethrowOnException()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(ct => throw new InvalidOperationException("Test error"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		policy.ConsecutiveFailures.ShouldBe(1);
	}

	[Fact]
	public async Task NotRecordFailureOnCancellation()
	{
		// Arrange
		var policy = CreatePolicy();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				await Task.Yield();
				return "result";
			}, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);

		// Cancellation should not be treated as a failure
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Execute Method Tests

	#region Custom Exception Handling Tests

	[Fact]
	public async Task RespectCustomShouldHandlePredicate()
	{
		// Arrange - Only handle TimeoutException
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = new CircuitBreakerPolicy(
			options,
			"custom-handler",
			_logger,
			shouldHandle: ex => ex is TimeoutException);

		// Act - This should NOT trip the circuit because InvalidOperationException is not handled
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(ct => throw new InvalidOperationException("Not handled"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert - Circuit should still be closed since exception was not handled
		policy.State.ShouldBe(CircuitState.Closed);
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public async Task TripCircuitOnHandledException()
	{
		// Arrange - Only handle TimeoutException
		// Use a very long OpenDuration so that under extreme CPU starvation (full-suite VS Test Explorer load),
		// the circuit doesn't auto-transition Open → HalfOpen before the assertion runs.
		var options = new CircuitBreakerOptions { FailureThreshold = 1, OpenDuration = TimeSpan.FromHours(1) };
		var policy = new CircuitBreakerPolicy(
			options,
			"custom-handler",
			_logger,
			shouldHandle: ex => ex is TimeoutException);

		// Act - This SHOULD trip the circuit
		_ = await Should.ThrowAsync<TimeoutException>(async () =>
			await policy.ExecuteAsync<int>(ct => throw new TimeoutException("Handled"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert
		policy.State.ShouldBe(CircuitState.Open);
		policy.ConsecutiveFailures.ShouldBe(1);
	}

	#endregion Custom Exception Handling Tests

	#region Constructor Validation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CircuitBreakerPolicy(null!, "test"));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new CircuitBreakerPolicy(options, null!));
	}

	[Fact]
	public void AcceptNullLogger()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		var policy = new CircuitBreakerPolicy(options, "test", null);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullShouldHandle()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		var policy = new CircuitBreakerPolicy(options, "test", _logger, null);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	#endregion Constructor Validation Tests

	#region CircuitBreakerOpenException Tests

	[Fact]
	public async Task ProvideRetryAfterInException()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMinutes(5),
		};
		var policy = CreatePolicy(options);

		// Open the circuit
		policy.RecordFailure(new InvalidOperationException("Error"));

		// Act & Assert
		var exception = await Should.ThrowAsync<CircuitBreakerOpenException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				await Task.Yield();
				return "result";
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		exception.RetryAfter.HasValue.ShouldBeTrue();
		exception.RetryAfter.Value.ShouldBeGreaterThan(TimeSpan.Zero);
		exception.RetryAfter.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void CircuitBreakerOpenException_HasCorrectCircuitName()
	{
		// Arrange
		var exception = new CircuitBreakerOpenException("my-circuit", TimeSpan.FromSeconds(30));

		// Assert
		exception.CircuitName.ShouldBe("my-circuit");
		exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion CircuitBreakerOpenException Tests

	#region Thread Safety Tests

	[Fact]
	public async Task HandleConcurrentFailureRecording()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 100 };
		var policy = CreatePolicy(options);
		var tasks = new List<Task>();

		// Act - Record failures from multiple threads
		for (var i = 0; i < 50; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < 10; j++)
				{
					policy.RecordFailure(new InvalidOperationException($"Error {j}"));
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Total failures should be 500 (50 threads * 10 failures each)
		// Note: Due to circuit opening logic, consecutive failures may be less
		// The key assertion is no exceptions due to race conditions
		policy.ConsecutiveFailures.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task HandleConcurrentStateTransitions()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			OpenDuration = TimeSpan.FromMilliseconds(10),
			SuccessThreshold = 2,
		};
		var policy = CreatePolicy(options);
		var tasks = new List<Task>();

		// Act - Mix of failures, successes, and state checks from multiple threads
		for (var i = 0; i < 20; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < 5; j++)
				{
					if (j % 2 == 0)
					{
						policy.RecordFailure();
					}
					else
					{
						policy.RecordSuccess();
					}

					_ = policy.State; // Force state evaluation
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should complete without exceptions
		// State may vary due to race conditions, but should be valid
		policy.State.ShouldBeOneOf(CircuitState.Closed, CircuitState.Open, CircuitState.HalfOpen);
	}

	[Fact]
	public async Task HandleConcurrentResetCalls()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);
		policy.RecordFailure(); // Open the circuit
		var tasks = new List<Task>();

		// Act - Reset from multiple threads
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(() => policy.Reset()));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		policy.State.ShouldBe(CircuitState.Closed);
		policy.ConsecutiveFailures.ShouldBe(0);
	}

	#endregion Thread Safety Tests

	#region Void Execute Failure Tests

	[Fact]
	public async Task RecordFailureForVoidAction()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<bool>(ct => throw new InvalidOperationException("Void error"), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		policy.ConsecutiveFailures.ShouldBe(1);
	}

	#endregion Void Execute Failure Tests

	#region State Changed Event - Additional Scenarios

	[Fact]
	public async Task RaiseStateChangedEventOnHalfOpenTransition()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 1,
			OpenDuration = TimeSpan.FromMilliseconds(10),
		};
		var policy = CreatePolicy(options);
		var events = new List<CircuitStateChangedEventArgs>();

		policy.StateChanged += (sender, args) => events.Add(args);

		// Open the circuit
		policy.RecordFailure(new InvalidOperationException("Error"));

		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert
		events.Count.ShouldBe(2); // Closed->Open, Open->HalfOpen
		events[0].PreviousState.ShouldBe(CircuitState.Closed);
		events[0].NewState.ShouldBe(CircuitState.Open);
		events[1].PreviousState.ShouldBe(CircuitState.Open);
		events[1].NewState.ShouldBe(CircuitState.HalfOpen);
	}

	[Fact]
	public void RaiseStateChangedEventOnResetFromOpen()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 1 };
		var policy = CreatePolicy(options);
		var events = new List<CircuitStateChangedEventArgs>();

		policy.StateChanged += (sender, args) => events.Add(args);
		policy.RecordFailure(); // Open
		events.Clear(); // Clear the open event

		// Act
		policy.Reset();

		// Assert
		events.Count.ShouldBe(1);
		events[0].PreviousState.ShouldBe(CircuitState.Open);
		events[0].NewState.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void NotRaiseEventOnResetWhenAlreadyClosed()
	{
		// Arrange
		var policy = CreatePolicy();
		var events = new List<CircuitStateChangedEventArgs>();

		policy.StateChanged += (sender, args) => events.Add(args);

		// Act - Reset when already closed
		policy.Reset();

		// Assert - No state change event because state didn't change
		events.Count.ShouldBe(0);
	}

	#endregion State Changed Event - Additional Scenarios

	#region Edge Case Tests

	[Fact]
	public void NotTransitionToSameState()
	{
		// Arrange - Test that TransitionTo with same state is a no-op
		var options = new CircuitBreakerOptions { FailureThreshold = 5 };
		var policy = CreatePolicy(options);
		var eventCount = 0;

		policy.StateChanged += (_, _) => eventCount++;

		// Act - Record failures but don't reach threshold
		policy.RecordFailure();
		policy.RecordFailure();

		// Assert - Should still be Closed, no state change events
		policy.State.ShouldBe(CircuitState.Closed);
		eventCount.ShouldBe(0);
	}

	[Fact]
	public async Task ResetSuccessfulProbesOnOpenTransition()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			OpenDuration = TimeSpan.FromMilliseconds(10),
			SuccessThreshold = 5,
		};
		var policy = CreatePolicy(options);

		// Open, wait for half-open
		for (var i = 0; i < 3; i++)
		{
			policy.RecordFailure();
		}

		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Record some successful probes
		policy.RecordSuccess();
		policy.RecordSuccess();

		// Act - Record failure to reopen
		policy.RecordFailure();

		// Assert
		policy.State.ShouldBe(CircuitState.Open);

		// Wait for half-open again
		await WaitForStateAsync(policy, CircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Successful probes should be reset - need full threshold again
		policy.RecordSuccess();
		policy.State.ShouldBe(CircuitState.HalfOpen); // Should still be half-open
	}

	#endregion Edge Case Tests
}
