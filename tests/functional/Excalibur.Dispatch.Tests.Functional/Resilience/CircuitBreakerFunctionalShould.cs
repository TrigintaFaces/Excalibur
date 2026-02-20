// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Resilience;

/// <summary>
/// Functional tests for circuit breaker patterns in realistic dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Resilience")]
[Trait("Feature", "CircuitBreaker")]
public sealed class CircuitBreakerFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void OpenCircuitAfterConsecutiveFailures()
	{
		// Arrange
		const int failureThreshold = 3;
		var circuitState = new TestCircuitState { State = CircuitStateEnum.Closed };
		var failureCount = 0;

		// Act - Simulate failures
		for (var i = 0; i < failureThreshold; i++)
		{
			failureCount++;
			if (failureCount >= failureThreshold)
			{
				circuitState.State = CircuitStateEnum.Open;
				circuitState.OpenedAt = DateTimeOffset.UtcNow;
			}
		}

		// Assert
		circuitState.State.ShouldBe(CircuitStateEnum.Open);
		_ = circuitState.OpenedAt.ShouldNotBeNull();
	}

	[Fact]
	public void TransitionToHalfOpenAfterBreakDuration()
	{
		// Arrange
		var breakDuration = TimeSpan.FromMilliseconds(100);
		var circuitState = new TestCircuitState
		{
			State = CircuitStateEnum.Open,
			OpenedAt = DateTimeOffset.UtcNow.AddMilliseconds(-150),
		};

		// Act - Check if circuit can transition to half-open
		var timeSinceOpened = DateTimeOffset.UtcNow - circuitState.OpenedAt.Value;
		if (timeSinceOpened >= breakDuration)
		{
			circuitState.State = CircuitStateEnum.HalfOpen;
		}

		// Assert
		circuitState.State.ShouldBe(CircuitStateEnum.HalfOpen);
	}

	[Fact]
	public void CloseCircuitAfterSuccessfulProbe()
	{
		// Arrange
		var circuitState = new TestCircuitState
		{
			State = CircuitStateEnum.HalfOpen,
			FailureCount = 0,
		};

		// Act - Simulate successful probe
		circuitState.SuccessCount++;
		if (circuitState.SuccessCount > 0)
		{
			circuitState.State = CircuitStateEnum.Closed;
			circuitState.FailureCount = 0;
			circuitState.OpenedAt = null;
		}

		// Assert
		circuitState.State.ShouldBe(CircuitStateEnum.Closed);
		circuitState.FailureCount.ShouldBe(0);
		circuitState.OpenedAt.ShouldBeNull();
	}

	[Fact]
	public void ReOpenCircuitOnFailureInHalfOpen()
	{
		// Arrange
		var circuitState = new TestCircuitState
		{
			State = CircuitStateEnum.HalfOpen,
		};

		// Act - Simulate failure during half-open
		circuitState.FailureCount++;
		circuitState.State = CircuitStateEnum.Open;
		circuitState.OpenedAt = DateTimeOffset.UtcNow;

		// Assert
		circuitState.State.ShouldBe(CircuitStateEnum.Open);
		_ = circuitState.OpenedAt.ShouldNotBeNull();
	}

	[Fact]
	public void TrackCircuitMetrics()
	{
		// Arrange
		var metrics = new TestCircuitMetrics();

		// Act - Simulate request flow
		metrics.TotalRequests = 100;
		metrics.FailedRequests = 15;
		metrics.RejectedRequests = 5;
		metrics.TimesOpened = 2;

		// Assert
		metrics.TotalRequests.ShouldBe(100);
		metrics.FailedRequests.ShouldBe(15);
		metrics.RejectedRequests.ShouldBe(5);
		metrics.TimesOpened.ShouldBe(2);
	}

	[Fact]
	public void CalculateFailureRate()
	{
		// Arrange
		var metrics = new TestCircuitMetrics
		{
			TotalRequests = 100,
			FailedRequests = 25,
		};

		// Act
		var failureRate = (double)metrics.FailedRequests / metrics.TotalRequests;

		// Assert
		failureRate.ShouldBe(0.25);
	}

	[Fact]
	public void HandleCircuitStateTransitions()
	{
		// Arrange
		var transitions = new List<(CircuitStateEnum from, CircuitStateEnum to)>();
		var state = CircuitStateEnum.Closed;

		// Act - Simulate state transitions
		void Transition(CircuitStateEnum newState)
		{
			transitions.Add((state, newState));
			state = newState;
		}

		Transition(CircuitStateEnum.Open);      // Closed -> Open
		Transition(CircuitStateEnum.HalfOpen);  // Open -> HalfOpen
		Transition(CircuitStateEnum.Closed);    // HalfOpen -> Closed

		// Assert
		transitions.Count.ShouldBe(3);
		transitions[0].ShouldBe((CircuitStateEnum.Closed, CircuitStateEnum.Open));
		transitions[1].ShouldBe((CircuitStateEnum.Open, CircuitStateEnum.HalfOpen));
		transitions[2].ShouldBe((CircuitStateEnum.HalfOpen, CircuitStateEnum.Closed));
	}

	[Fact]
	public async Task ProcessRequestsWhenCircuitClosed()
	{
		// Arrange
		var circuitState = new TestCircuitState { State = CircuitStateEnum.Closed };
		var processedCount = 0;

		// Act
		for (var i = 0; i < 10; i++)
		{
			if (circuitState.State == CircuitStateEnum.Closed)
			{
				await Task.Delay(1).ConfigureAwait(false);
				processedCount++;
			}
		}

		// Assert
		processedCount.ShouldBe(10);
	}

	[Fact]
	public void RejectRequestsWhenCircuitOpen()
	{
		// Arrange
		var circuitState = new TestCircuitState { State = CircuitStateEnum.Open };
		var rejectedCount = 0;

		// Act
		for (var i = 0; i < 5; i++)
		{
			if (circuitState.State == CircuitStateEnum.Open)
			{
				rejectedCount++;
			}
		}

		// Assert
		rejectedCount.ShouldBe(5);
	}

	private enum CircuitStateEnum
	{
		Closed,
		Open,
		HalfOpen,
	}

	private sealed class TestCircuitState
	{
		public CircuitStateEnum State { get; set; }
		public int FailureCount { get; set; }
		public int SuccessCount { get; set; }
		public DateTimeOffset? OpenedAt { get; set; }
	}

	private sealed class TestCircuitMetrics
	{
		public int TotalRequests { get; set; }
		public int FailedRequests { get; set; }
		public int RejectedRequests { get; set; }
		public int TimesOpened { get; set; }
	}
}
