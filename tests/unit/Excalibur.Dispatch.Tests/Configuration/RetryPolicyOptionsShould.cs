// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new RetryPolicyOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryStrategy.ShouldBe(RetryStrategy.FixedDelay);
		options.Backoff.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Backoff.MaxDelay.ShouldBe(TimeSpan.FromMinutes(30));
		options.Backoff.BackoffMultiplier.ShouldBe(2.0);
		options.Backoff.EnableJitter.ShouldBeFalse();
		options.Backoff.JitterFactor.ShouldBe(0.1);
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.RetriableExceptions.ShouldBeEmpty();
		options.NonRetriableExceptions.ShouldBeEmpty();
		options.CircuitBreaker.EnableCircuitBreaker.ShouldBeFalse();
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(5);
		options.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromSeconds(30));
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
			Backoff =
			{
				BaseDelay = TimeSpan.FromMilliseconds(500),
				MaxDelay = TimeSpan.FromMinutes(10),
				BackoffMultiplier = 3.0,
				EnableJitter = true,
				JitterFactor = 0.5,
			},
			Timeout = TimeSpan.FromMinutes(1),
			CircuitBreaker =
			{
				EnableCircuitBreaker = true,
				CircuitBreakerThreshold = 10,
				CircuitBreakerDuration = TimeSpan.FromMinutes(2),
			},
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryStrategy.ShouldBe(RetryStrategy.ExponentialBackoff);
		options.Backoff.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.Backoff.MaxDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.Backoff.BackoffMultiplier.ShouldBe(3.0);
		options.Backoff.EnableJitter.ShouldBeTrue();
		options.Backoff.JitterFactor.ShouldBe(0.5);
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.CircuitBreaker.EnableCircuitBreaker.ShouldBeTrue();
		options.CircuitBreaker.CircuitBreakerThreshold.ShouldBe(10);
		options.CircuitBreaker.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}
}
