// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CircuitBreakerMetricsShould
{
	[Fact]
	public void HaveDefaultZeroValues()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.TotalRequests.ShouldBe(0);
		metrics.SuccessfulRequests.ShouldBe(0);
		metrics.FailedRequests.ShouldBe(0);
		metrics.RejectedRequests.ShouldBe(0);
		metrics.FallbackExecutions.ShouldBe(0);
		metrics.ConsecutiveFailures.ShouldBe(0);
		metrics.ConsecutiveSuccesses.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultAverageResponseTime()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.AverageResponseTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveDefaultCurrentState()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.CurrentState.ShouldBe(default(ResilienceState));
	}

	[Fact]
	public void CalculateSuccessRateAsZeroWhenNoRequests()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.SuccessRate.ShouldBe(0);
	}

	[Fact]
	public void CalculateSuccessRateCorrectly()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 100,
			SuccessfulRequests = 85,
		};

		// Act
		var rate = metrics.SuccessRate;

		// Assert
		rate.ShouldBe(0.85);
	}

	[Fact]
	public void CalculateFullSuccessRate()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 100,
			SuccessfulRequests = 100,
		};

		// Assert
		metrics.SuccessRate.ShouldBe(1.0);
	}

	[Fact]
	public void CalculateZeroSuccessRate()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 100,
			SuccessfulRequests = 0,
		};

		// Assert
		metrics.SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingTotalRequests()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.TotalRequests = 1000;

		// Assert
		metrics.TotalRequests.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingSuccessfulRequests()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.SuccessfulRequests = 900;

		// Assert
		metrics.SuccessfulRequests.ShouldBe(900);
	}

	[Fact]
	public void AllowSettingFailedRequests()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.FailedRequests = 100;

		// Assert
		metrics.FailedRequests.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingRejectedRequests()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.RejectedRequests = 50;

		// Assert
		metrics.RejectedRequests.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingFallbackExecutions()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.FallbackExecutions = 25;

		// Assert
		metrics.FallbackExecutions.ShouldBe(25);
	}

	[Fact]
	public void AllowSettingAverageResponseTime()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();
		var responseTime = TimeSpan.FromMilliseconds(150);

		// Act
		metrics.AverageResponseTime = responseTime;

		// Assert
		metrics.AverageResponseTime.ShouldBe(responseTime);
	}

	[Fact]
	public void AllowSettingCurrentState()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.CurrentState = ResilienceState.HalfOpen;

		// Assert
		metrics.CurrentState.ShouldBe(ResilienceState.HalfOpen);
	}

	[Fact]
	public void AllowSettingConsecutiveFailures()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.ConsecutiveFailures = 5;

		// Assert
		metrics.ConsecutiveFailures.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingConsecutiveSuccesses()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.ConsecutiveSuccesses = 10;

		// Assert
		metrics.ConsecutiveSuccesses.ShouldBe(10);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 1000,
			SuccessfulRequests = 850,
			FailedRequests = 100,
			RejectedRequests = 50,
			FallbackExecutions = 30,
			AverageResponseTime = TimeSpan.FromMilliseconds(100),
			CurrentState = ResilienceState.Closed,
			ConsecutiveFailures = 0,
			ConsecutiveSuccesses = 15,
		};

		// Assert
		metrics.TotalRequests.ShouldBe(1000);
		metrics.SuccessfulRequests.ShouldBe(850);
		metrics.FailedRequests.ShouldBe(100);
		metrics.RejectedRequests.ShouldBe(50);
		metrics.FallbackExecutions.ShouldBe(30);
		metrics.AverageResponseTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		metrics.CurrentState.ShouldBe(ResilienceState.Closed);
		metrics.ConsecutiveFailures.ShouldBe(0);
		metrics.ConsecutiveSuccesses.ShouldBe(15);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousTotalRequestsValues(long value)
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.TotalRequests = value;

		// Assert
		metrics.TotalRequests.ShouldBe(value);
	}

	[Theory]
	[InlineData(ResilienceState.Closed)]
	[InlineData(ResilienceState.Open)]
	[InlineData(ResilienceState.HalfOpen)]
	public void AcceptVariousResilienceStates(ResilienceState state)
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act
		metrics.CurrentState = state;

		// Assert
		metrics.CurrentState.ShouldBe(state);
	}

	[Fact]
	public void TrackTypicalCircuitBreakerScenario()
	{
		// Arrange & Act - Simulate circuit breaker metrics
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 500,
			SuccessfulRequests = 400,
			FailedRequests = 80,
			RejectedRequests = 20,
			AverageResponseTime = TimeSpan.FromMilliseconds(50),
			CurrentState = ResilienceState.Closed,
			ConsecutiveSuccesses = 10,
		};

		// Assert
		metrics.SuccessRate.ShouldBe(0.8);
		(metrics.SuccessfulRequests + metrics.FailedRequests + metrics.RejectedRequests)
			.ShouldBe(metrics.TotalRequests);
	}
}
