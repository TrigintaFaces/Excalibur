// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="RetryPolicyOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class RetryPolicyOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxRetryAttempts_Is3()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_MaxAttempts_Is3()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert - MaxAttempts is alias for MaxRetryAttempts
		options.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_RetryStrategy_IsFixedDelay()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.RetryStrategy.ShouldBe(RetryStrategy.FixedDelay);
	}

	[Fact]
	public void Default_BaseDelay_Is1Second()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_MaxDelay_Is30Minutes()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Default_BackoffMultiplier_Is2()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.BackoffMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void Default_EnableJitter_IsFalse()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.EnableJitter.ShouldBeFalse();
	}

	[Fact]
	public void Default_JitterFactor_Is0_1()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.JitterFactor.ShouldBe(0.1);
	}

	[Fact]
	public void Default_Timeout_Is30Seconds()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_RetriableExceptions_IsEmpty()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		_ = options.RetriableExceptions.ShouldNotBeNull();
		options.RetriableExceptions.ShouldBeEmpty();
	}

	[Fact]
	public void Default_NonRetriableExceptions_IsEmpty()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		_ = options.NonRetriableExceptions.ShouldNotBeNull();
		options.NonRetriableExceptions.ShouldBeEmpty();
	}

	[Fact]
	public void Default_EnableCircuitBreaker_IsFalse()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.EnableCircuitBreaker.ShouldBeFalse();
	}

	[Fact]
	public void Default_CircuitBreakerThreshold_Is5()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.CircuitBreakerThreshold.ShouldBe(5);
	}

	[Fact]
	public void Default_CircuitBreakerFailureThreshold_Is5()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert - Alias for CircuitBreakerThreshold
		options.CircuitBreakerFailureThreshold.ShouldBe(5);
	}

	[Fact]
	public void Default_CircuitBreakerDuration_Is30Seconds()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_CircuitBreakerRecoveryTimeout_Is30Seconds()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert - Alias for CircuitBreakerDuration
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxRetryAttempts_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxAttempts.ShouldBe(5); // Alias should reflect same value
	}

	[Fact]
	public void MaxAttempts_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.MaxAttempts = 10;

		// Assert
		options.MaxAttempts.ShouldBe(10);
		options.MaxRetryAttempts.ShouldBe(10); // Main property should reflect same value
	}

	[Fact]
	public void RetryStrategy_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.RetryStrategy = RetryStrategy.ExponentialBackoff;

		// Assert
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
	}

	[Fact]
	public void BaseDelay_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.BaseDelay = TimeSpan.FromSeconds(5);

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void EnableJitter_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.EnableJitter = true;

		// Assert
		options.EnableJitter.ShouldBeTrue();
	}

	[Fact]
	public void RetriableExceptions_CanAddItems()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		_ = options.RetriableExceptions.Add(typeof(TimeoutException));

		// Assert
		options.RetriableExceptions.Count.ShouldBe(1);
		options.RetriableExceptions.ShouldContain(typeof(TimeoutException));
	}

	[Fact]
	public void NonRetriableExceptions_CanAddItems()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		_ = options.NonRetriableExceptions.Add(typeof(ArgumentException));

		// Assert
		options.NonRetriableExceptions.Count.ShouldBe(1);
		options.NonRetriableExceptions.ShouldContain(typeof(ArgumentException));
	}

	[Fact]
	public void CircuitBreakerThreshold_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerThreshold = 3;

		// Assert
		options.CircuitBreakerThreshold.ShouldBe(3);
		options.CircuitBreakerFailureThreshold.ShouldBe(3); // Alias should reflect same value
	}

	[Fact]
	public void CircuitBreakerFailureThreshold_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerFailureThreshold = 7;

		// Assert
		options.CircuitBreakerFailureThreshold.ShouldBe(7);
		options.CircuitBreakerThreshold.ShouldBe(7); // Main property should reflect same value
	}

	[Fact]
	public void CircuitBreakerDuration_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerDuration = TimeSpan.FromMinutes(1);

		// Assert
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromMinutes(1)); // Alias should reflect same value
	}

	[Fact]
	public void CircuitBreakerRecoveryTimeout_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreakerRecoveryTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.CircuitBreakerRecoveryTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(2)); // Main property should reflect same value
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
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
			JitterFactor = 0.2,
			Timeout = TimeSpan.FromMinutes(1),
			EnableCircuitBreaker = true,
			CircuitBreakerThreshold = 3,
			CircuitBreakerDuration = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.BackoffMultiplier.ShouldBe(3.0);
		options.EnableJitter.ShouldBeTrue();
		options.JitterFactor.ShouldBe(0.2);
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreakerThreshold.ShouldBe(3);
		options.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForExponentialBackoff_UsesProperStrategy()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			RetryStrategy = RetryStrategy.ExponentialBackoff,
			BaseDelay = TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2.0,
			MaxDelay = TimeSpan.FromMinutes(5),
			EnableJitter = true,
		};

		// Assert
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.EnableJitter.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForAggressiveRetry_HasHighAttemptCount()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 10,
			BaseDelay = TimeSpan.FromMilliseconds(100),
			RetryStrategy = RetryStrategy.FixedDelay,
		};

		// Assert
		options.MaxRetryAttempts.ShouldBeGreaterThan(5);
		options.BaseDelay.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Options_WithCircuitBreaker_ConfiguresProtection()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			EnableCircuitBreaker = true,
			CircuitBreakerThreshold = 3,
			CircuitBreakerDuration = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreakerThreshold.ShouldBeLessThan(5);
	}

	#endregion
}
