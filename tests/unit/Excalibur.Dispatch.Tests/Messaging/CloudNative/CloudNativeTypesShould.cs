// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CloudNativeTypesShould
{
	// Sprint 682 T.24: CloudNative.CircuitBreakerOpenException deleted.
	// Tests now target canonical Resilience.CircuitBreakerOpenException.

	[Fact]
	public void CircuitBreakerOpenException_DefaultConstructor()
	{
		// Act
		var ex = new CircuitBreakerOpenException();

		// Assert
		ex.Message.ShouldNotBeNull();
		ex.InnerException.ShouldBeNull();
		ex.CircuitName.ShouldBeNull();
		ex.RetryAfter.ShouldBeNull();
	}

	[Fact]
	public void CircuitBreakerOpenException_NameConstructor()
	{
		// Act
		var ex = new CircuitBreakerOpenException("my-breaker");

		// Assert
		ex.CircuitName.ShouldBe("my-breaker");
		ex.Message.ShouldContain("my-breaker");
	}

	[Fact]
	public void CircuitBreakerOpenException_NameAndRetryAfterConstructor()
	{
		// Act
		var ex = new CircuitBreakerOpenException("my-breaker", TimeSpan.FromSeconds(30));

		// Assert
		ex.CircuitName.ShouldBe("my-breaker");
		ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CircuitBreakerOpenException_MessageAndInnerConstructor()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new CircuitBreakerOpenException("circuit is open", inner);

		// Assert
		ex.Message.ShouldBe("circuit is open");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CircuitBreakerMetrics_DefaultValues()
	{
		// Act
		var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.TotalRequests.ShouldBe(0);
		metrics.SuccessfulRequests.ShouldBe(0);
		metrics.FailedRequests.ShouldBe(0);
		metrics.RejectedRequests.ShouldBe(0);
		metrics.FallbackExecutions.ShouldBe(0);
		metrics.SuccessRate.ShouldBe(0);
		metrics.AverageResponseTime.ShouldBe(TimeSpan.Zero);
		metrics.CurrentState.ShouldBe(ResilienceState.Closed);
		metrics.ConsecutiveFailures.ShouldBe(0);
		metrics.ConsecutiveSuccesses.ShouldBe(0);
	}

	[Fact]
	public void CircuitBreakerMetrics_SetAllProperties()
	{
		// Arrange & Act
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 100,
			SuccessfulRequests = 90,
			FailedRequests = 10,
			RejectedRequests = 5,
			FallbackExecutions = 3,
			AverageResponseTime = TimeSpan.FromMilliseconds(50),
			CurrentState = ResilienceState.HalfOpen,
			ConsecutiveFailures = 2,
			ConsecutiveSuccesses = 0,
		};

		// Assert
		metrics.TotalRequests.ShouldBe(100);
		metrics.SuccessfulRequests.ShouldBe(90);
		metrics.FailedRequests.ShouldBe(10);
		metrics.RejectedRequests.ShouldBe(5);
		metrics.FallbackExecutions.ShouldBe(3);
		metrics.AverageResponseTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		metrics.CurrentState.ShouldBe(ResilienceState.HalfOpen);
		metrics.ConsecutiveFailures.ShouldBe(2);
		metrics.ConsecutiveSuccesses.ShouldBe(0);
	}

	[Fact]
	public void CircuitBreakerMetrics_SuccessRate_CalculateCorrectly()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 100,
			SuccessfulRequests = 75,
		};

		// Act & Assert
		metrics.SuccessRate.ShouldBe(0.75);
	}

	[Fact]
	public void CircuitBreakerMetrics_SuccessRate_ZeroTotalRequests()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics
		{
			TotalRequests = 0,
			SuccessfulRequests = 0,
		};

		// Act & Assert
		metrics.SuccessRate.ShouldBe(0);
	}

	[Fact]
	public void PatternMetrics_DefaultValues()
	{
		// Act
		var metrics = new PatternMetrics();

		// Assert
		metrics.TotalOperations.ShouldBe(0);
		metrics.SuccessfulOperations.ShouldBe(0);
		metrics.FailedOperations.ShouldBe(0);
		metrics.AverageOperationTime.ShouldBe(TimeSpan.Zero);
		metrics.SuccessRate.ShouldBe(0);
		metrics.LastUpdated.ShouldNotBe(default);
		metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void PatternMetrics_SuccessRate_CalculateCorrectly()
	{
		// Arrange
		var metrics = new PatternMetrics
		{
			TotalOperations = 200,
			SuccessfulOperations = 180,
		};

		// Act & Assert
		metrics.SuccessRate.ShouldBe(0.9);
	}

	[Fact]
	public void PatternMetrics_SetProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var metrics = new PatternMetrics
		{
			TotalOperations = 50,
			SuccessfulOperations = 45,
			FailedOperations = 5,
			AverageOperationTime = TimeSpan.FromMilliseconds(100),
			LastUpdated = timestamp,
		};
		metrics.CustomMetrics["p99"] = 200.0;

		// Assert
		metrics.TotalOperations.ShouldBe(50);
		metrics.SuccessfulOperations.ShouldBe(45);
		metrics.FailedOperations.ShouldBe(5);
		metrics.AverageOperationTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		metrics.LastUpdated.ShouldBe(timestamp);
		metrics.CustomMetrics["p99"].ShouldBe(200.0);
	}

	[Fact]
	public void PatternStateChange_DefaultValues()
	{
		// Act
		var change = new PatternStateChange();

		// Assert
		change.Timestamp.ShouldNotBe(default);
		change.PreviousState.ShouldBeNull();
		change.NewState.ShouldBeNull();
		change.Reason.ShouldBe(string.Empty);
		change.Context.ShouldNotBeNull();
		change.Context.ShouldBeEmpty();
	}

	[Fact]
	public void PatternStateChange_SetProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var change = new PatternStateChange
		{
			Timestamp = timestamp,
			PreviousState = ResilienceState.Closed,
			NewState = ResilienceState.Open,
			Reason = "Failure threshold exceeded",
		};
		change.Context["failures"] = 10;

		// Assert
		change.Timestamp.ShouldBe(timestamp);
		change.PreviousState.ShouldBe(ResilienceState.Closed);
		change.NewState.ShouldBe(ResilienceState.Open);
		change.Reason.ShouldBe("Failure threshold exceeded");
		change.Context["failures"].ShouldBe(10);
	}

	[Fact]
	public void ResilienceState_HaveExpectedValues()
	{
		ResilienceState.Closed.ShouldBe((ResilienceState)0);
		ResilienceState.Open.ShouldBe((ResilienceState)1);
		ResilienceState.HalfOpen.ShouldBe((ResilienceState)2);
	}

	[Fact]
	public void PatternHealthStatus_HaveExpectedValues()
	{
		PatternHealthStatus.Unknown.ShouldBe((PatternHealthStatus)0);
		PatternHealthStatus.Healthy.ShouldBe((PatternHealthStatus)1);
		PatternHealthStatus.Degraded.ShouldBe((PatternHealthStatus)2);
		PatternHealthStatus.Unhealthy.ShouldBe((PatternHealthStatus)3);
		PatternHealthStatus.Critical.ShouldBe((PatternHealthStatus)4);
	}
}
