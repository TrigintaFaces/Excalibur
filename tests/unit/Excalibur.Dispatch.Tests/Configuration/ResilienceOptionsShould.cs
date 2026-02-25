// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResilienceOptionsShould
{
	// --- CircuitBreakerOptions ---

	[Fact]
	public void CircuitBreakerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.FailureThreshold.ShouldBe(5);
		options.SuccessThreshold.ShouldBe(3);
		options.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxHalfOpenTests.ShouldBe(3);
		options.CircuitKeySelector.ShouldBeNull();
	}

	[Fact]
	public void CircuitBreakerOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			SuccessThreshold = 5,
			OpenDuration = TimeSpan.FromMinutes(1),
			OperationTimeout = TimeSpan.FromSeconds(10),
			MaxHalfOpenTests = 5,
			CircuitKeySelector = _ => "test-key",
		};

		// Assert
		options.FailureThreshold.ShouldBe(10);
		options.SuccessThreshold.ShouldBe(5);
		options.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxHalfOpenTests.ShouldBe(5);
		options.CircuitKeySelector.ShouldNotBeNull();
	}

	// --- RetryOptions ---

	[Fact]
	public void RetryOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new RetryOptions();

		// Assert
		options.MaxAttempts.ShouldBe(3);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
		options.BackoffMultiplier.ShouldBe(2.0);
		options.JitterFactor.ShouldBe(0.1);
		options.UseJitter.ShouldBeTrue();
		options.RetryableExceptions.ShouldNotBeNull();
		options.RetryableExceptions.ShouldBeEmpty();
		options.NonRetryableExceptions.ShouldNotBeNull();
		options.NonRetryableExceptions.Count.ShouldBe(3);
	}

	[Fact]
	public void RetryOptions_NonRetryableExceptions_ContainExpectedDefaults()
	{
		// Act
		var options = new RetryOptions();

		// Assert
		options.NonRetryableExceptions.ShouldContain(typeof(ArgumentException));
		options.NonRetryableExceptions.ShouldContain(typeof(ArgumentNullException));
		options.NonRetryableExceptions.ShouldContain(typeof(InvalidOperationException));
	}

	[Fact]
	public void RetryOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(1),
			BackoffStrategy = BackoffStrategy.Linear,
			BackoffMultiplier = 1.5,
			JitterFactor = 0.2,
			UseJitter = false,
		};

		// Assert
		options.MaxAttempts.ShouldBe(5);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
		options.BackoffMultiplier.ShouldBe(1.5);
		options.JitterFactor.ShouldBe(0.2);
		options.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void RetryOptions_RetryableExceptions_CanAddEntries()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.RetryableExceptions.Add(typeof(TimeoutException));

		// Assert
		options.RetryableExceptions.Count.ShouldBe(1);
		options.RetryableExceptions.ShouldContain(typeof(TimeoutException));
	}

	// --- RetryPolicyOptions ---

	[Fact]
	public void RetryPolicyOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryStrategy.ShouldBe(RetryStrategy.FixedDelay);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
		options.BackoffMultiplier.ShouldBe(2.0);
		options.EnableJitter.ShouldBeFalse();
		options.JitterFactor.ShouldBe(0.1);
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.RetriableExceptions.ShouldNotBeNull();
		options.RetriableExceptions.ShouldBeEmpty();
		options.NonRetriableExceptions.ShouldNotBeNull();
		options.NonRetriableExceptions.ShouldBeEmpty();
		options.EnableCircuitBreaker.ShouldBeFalse();
		options.CircuitBreakerThreshold.ShouldBe(5);
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void RetryPolicyOptions_MaxAttempts_IsAliasForMaxRetryAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act - set via alias
		options.MaxAttempts = 7;

		// Assert - both return same value
		options.MaxRetryAttempts.ShouldBe(7);
		options.MaxAttempts.ShouldBe(7);

		// Act - set via canonical
		options.MaxRetryAttempts = 10;

		// Assert - alias reflects change
		options.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void RetryPolicyOptions_CircuitBreakerFailureThreshold_IsAliasForCircuitBreakerThreshold()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act - set via alias
		options.CircuitBreakerFailureThreshold = 10;

		// Assert - both return same value
		options.CircuitBreakerThreshold.ShouldBe(10);
		options.CircuitBreakerFailureThreshold.ShouldBe(10);

		// Act - set via canonical
		options.CircuitBreakerThreshold = 20;

		// Assert - alias reflects change
		options.CircuitBreakerFailureThreshold.ShouldBe(20);
	}

	[Fact]
	public void RetryPolicyOptions_CircuitBreakerRecoveryTimeout_IsAliasForCircuitBreakerDuration()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act - set via alias
		options.CircuitBreakerRecoveryTimeout = TimeSpan.FromMinutes(2);

		// Assert - both return same value
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(2));
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromMinutes(2));

		// Act - set via canonical
		options.CircuitBreakerDuration = TimeSpan.FromMinutes(5);

		// Assert - alias reflects change
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void RetryPolicyOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			RetryStrategy = RetryStrategy.ExponentialBackoff,
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(5),
			BackoffMultiplier = 3.0,
			EnableJitter = true,
			JitterFactor = 0.3,
			Timeout = TimeSpan.FromMinutes(1),
			EnableCircuitBreaker = true,
			CircuitBreakerThreshold = 10,
			CircuitBreakerDuration = TimeSpan.FromMinutes(2),
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.BackoffMultiplier.ShouldBe(3.0);
		options.EnableJitter.ShouldBeTrue();
		options.JitterFactor.ShouldBe(0.3);
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreakerThreshold.ShouldBe(10);
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}

	// --- BackoffStrategy ---

	[Fact]
	public void BackoffStrategy_HaveExpectedValues()
	{
		// Assert
		BackoffStrategy.Fixed.ShouldBe((BackoffStrategy)0);
		BackoffStrategy.Linear.ShouldBe((BackoffStrategy)1);
		BackoffStrategy.Exponential.ShouldBe((BackoffStrategy)2);
		BackoffStrategy.ExponentialWithJitter.ShouldBe((BackoffStrategy)3);
		BackoffStrategy.Fibonacci.ShouldBe((BackoffStrategy)4);
	}

	[Fact]
	public void BackoffStrategy_HaveFiveValues()
	{
		// Act
		var values = Enum.GetValues<BackoffStrategy>();

		// Assert
		values.Length.ShouldBe(5);
	}

	// --- RetryStrategy ---

	[Fact]
	public void RetryStrategy_HaveExpectedValues()
	{
		// Assert
		RetryStrategy.FixedDelay.ShouldBe((RetryStrategy)0);
		RetryStrategy.ExponentialBackoff.ShouldBe((RetryStrategy)1);
	}

	[Fact]
	public void RetryStrategy_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<RetryStrategy>();

		// Assert
		values.Length.ShouldBe(2);
	}
}
