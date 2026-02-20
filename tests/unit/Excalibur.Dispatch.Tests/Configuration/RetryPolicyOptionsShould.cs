// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
		options.MaxAttempts.ShouldBe(3);
		options.RetryStrategy.ShouldBe(RetryStrategy.FixedDelay);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
		options.BackoffMultiplier.ShouldBe(2.0);
		options.EnableJitter.ShouldBeFalse();
		options.JitterFactor.ShouldBe(0.1);
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.RetriableExceptions.ShouldBeEmpty();
		options.NonRetriableExceptions.ShouldBeEmpty();
		options.EnableCircuitBreaker.ShouldBeFalse();
		options.CircuitBreakerThreshold.ShouldBe(5);
		options.CircuitBreakerFailureThreshold.ShouldBe(5);
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void MaxAttempts_IsAliasForMaxRetryAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxAttempts = 10;

		// Assert
		options.MaxRetryAttempts.ShouldBe(10);
	}

	[Fact]
	public void MaxRetryAttempts_UpdatesMaxAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxRetryAttempts = 7;

		// Assert
		options.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void CircuitBreakerFailureThreshold_IsAliasForCircuitBreakerThreshold()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerFailureThreshold = 15;

		// Assert
		options.CircuitBreakerThreshold.ShouldBe(15);
	}

	[Fact]
	public void CircuitBreakerRecoveryTimeout_IsAliasForCircuitBreakerDuration()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerRecoveryTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void RetriableExceptions_CanAddExceptionTypes()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.RetriableExceptions.Add(typeof(TimeoutException));
		options.RetriableExceptions.Add(typeof(InvalidOperationException));

		// Assert
		options.RetriableExceptions.Count.ShouldBe(2);
		options.RetriableExceptions.ShouldContain(typeof(TimeoutException));
	}

	[Fact]
	public void NonRetriableExceptions_CanAddExceptionTypes()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.NonRetriableExceptions.Add(typeof(ArgumentException));

		// Assert
		options.NonRetriableExceptions.Count.ShouldBe(1);
		options.NonRetriableExceptions.ShouldContain(typeof(ArgumentException));
	}

	[Fact]
	public void AllProperties_AreSettable()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			RetryStrategy = RetryStrategy.ExponentialBackoff,
			BaseDelay = TimeSpan.FromMilliseconds(500),
			MaxDelay = TimeSpan.FromMinutes(10),
			BackoffMultiplier = 3.0,
			EnableJitter = true,
			JitterFactor = 0.5,
			Timeout = TimeSpan.FromMinutes(1),
			EnableCircuitBreaker = true,
			CircuitBreakerThreshold = 10,
			CircuitBreakerDuration = TimeSpan.FromMinutes(2),
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.BackoffMultiplier.ShouldBe(3.0);
		options.EnableJitter.ShouldBeTrue();
		options.JitterFactor.ShouldBe(0.5);
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreakerThreshold.ShouldBe(10);
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
