// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ResilienceOptionsShould
{
	// --- CircuitBreakerOptions ---

	[Fact]
	public void CircuitBreakerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CircuitBreakerOptions();

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(5);
		options.MinimumThroughput.ShouldBe(5);
		options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.CircuitKeySelector.ShouldBeNull();
	}

	[Fact]
	public void CircuitBreakerOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 10,
			MinimumThroughput = 10,
			BreakDuration = TimeSpan.FromMinutes(1),
			OperationTimeout = TimeSpan.FromSeconds(10),
			CircuitKeySelector = _ => "test-key",
		};

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(10);
		options.MinimumThroughput.ShouldBe(10);
		options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.CircuitKeySelector.ShouldNotBeNull();
	}

	// --- RetryOptions ---

	[Fact]
	public void RetryOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new RetryOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
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
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(1),
			BackoffStrategy = BackoffStrategy.Linear,
			BackoffMultiplier = 1.5,
			JitterFactor = 0.2,
			UseJitter = false,
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
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
	public void BackoffStrategy_HaveSevenValues()
	{
		// Act
		var values = Enum.GetValues<BackoffStrategy>();

		// Assert — seven values since DecorrelatedJitter (=6) was added alongside FullJitter (=5).
		values.Length.ShouldBe(7);
		Enum.IsDefined(BackoffStrategy.FullJitter).ShouldBeTrue();
	}


}
