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
		options.Backoff.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_MaxDelay_Is30Minutes()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.Backoff.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Default_BackoffMultiplier_Is2()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.Backoff.BackoffMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void Default_EnableJitter_IsFalse()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.Backoff.EnableJitter.ShouldBeFalse();
	}

	[Fact]
	public void Default_JitterFactor_Is0_1()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.Backoff.JitterFactor.ShouldBe(0.1);
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
		options.CircuitBreaker.EnableCircuitBreaker.ShouldBeFalse();
	}

	[Fact]
	public void Default_CircuitBreakerThreshold_Is5()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(5);
	}

	[Fact]
	public void Default_CircuitBreakerDuration_Is30Seconds()
	{
		// Arrange & Act
		var options = new RetryPolicyOptions();

		// Assert
		options.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
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
		options.Backoff.BaseDelay = TimeSpan.FromSeconds(5);

		// Assert
		options.Backoff.BaseDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void EnableJitter_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.Backoff.EnableJitter = true;

		// Assert
		options.Backoff.EnableJitter.ShouldBeTrue();
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
		options.CircuitBreaker.CircuitBreakerThreshold = 3;

		// Assert
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(3);
	}

	[Fact]
	public void CircuitBreakerDuration_CanBeSet()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		options.CircuitBreaker.CircuitBreakerDuration = TimeSpan.FromMinutes(1);

		// Assert
		options.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
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
			Backoff =
			{
				BaseDelay = TimeSpan.FromSeconds(2),
				MaxDelay = TimeSpan.FromMinutes(5),
				BackoffMultiplier = 3.0,
				EnableJitter = true,
				JitterFactor = 0.2,
			},
			Timeout = TimeSpan.FromMinutes(1),
			CircuitBreaker =
			{
				EnableCircuitBreaker = true,
				CircuitBreakerThreshold = 3,
				CircuitBreakerDuration = TimeSpan.FromMinutes(1),
			},
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.Backoff.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.Backoff.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.Backoff.BackoffMultiplier.ShouldBe(3.0);
		options.Backoff.EnableJitter.ShouldBeTrue();
		options.Backoff.JitterFactor.ShouldBe(0.2);
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.CircuitBreaker.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(3);
		options.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
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
			Backoff =
			{
				BaseDelay = TimeSpan.FromSeconds(1),
				BackoffMultiplier = 2.0,
				MaxDelay = TimeSpan.FromMinutes(5),
				EnableJitter = true,
			},
		};

		// Assert
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.Backoff.EnableJitter.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForAggressiveRetry_HasHighAttemptCount()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 10,
			Backoff = { BaseDelay = TimeSpan.FromMilliseconds(100) },
			RetryStrategy = RetryStrategy.FixedDelay,
		};

		// Assert
		options.MaxRetryAttempts.ShouldBeGreaterThan(5);
		options.Backoff.BaseDelay.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Options_WithCircuitBreaker_ConfiguresProtection()
	{
		// Act
		var options = new RetryPolicyOptions
		{
			CircuitBreaker =
			{
				EnableCircuitBreaker = true,
				CircuitBreakerThreshold = 3,
				CircuitBreakerDuration = TimeSpan.FromSeconds(30),
			},
		};

		// Assert
		options.CircuitBreaker.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBeLessThan(5);
	}

	#endregion
}
