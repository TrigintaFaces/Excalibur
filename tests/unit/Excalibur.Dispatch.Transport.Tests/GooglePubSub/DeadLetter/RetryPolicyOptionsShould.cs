// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using PubSubRetryPolicyOptions = Excalibur.Dispatch.Transport.Google.RetryPolicyOptions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void HaveCorrectDefaultStrategyDefaults()
	{
		// Arrange & Act
		var options = new PubSubRetryPolicyOptions();

		// Assert
		options.DefaultStrategy.ShouldNotBeNull();
		options.DefaultStrategy.MaxRetryAttempts.ShouldBe(5);
		options.DefaultStrategy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
		options.DefaultStrategy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.DefaultStrategy.BackoffType.ShouldBe(BackoffType.Exponential);
		options.DefaultStrategy.JitterEnabled.ShouldBeTrue();
		options.DefaultStrategy.CircuitBreakerEnabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectTimeoutStrategyDefaults()
	{
		// Arrange & Act
		var options = new PubSubRetryPolicyOptions();

		// Assert
		options.TimeoutStrategy.ShouldNotBeNull();
		options.TimeoutStrategy.MaxRetryAttempts.ShouldBe(3);
		options.TimeoutStrategy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.TimeoutStrategy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.TimeoutStrategy.BackoffType.ShouldBe(BackoffType.Linear);
		options.TimeoutStrategy.JitterEnabled.ShouldBeTrue();
		options.TimeoutStrategy.CircuitBreakerEnabled.ShouldBeTrue();
		options.TimeoutStrategy.CircuitBreakerThreshold.ShouldBe(3);
		options.TimeoutStrategy.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveCorrectTransientErrorStrategyDefaults()
	{
		// Arrange & Act
		var options = new PubSubRetryPolicyOptions();

		// Assert
		options.TransientErrorStrategy.ShouldNotBeNull();
		options.TransientErrorStrategy.MaxRetryAttempts.ShouldBe(6);
		options.TransientErrorStrategy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.TransientErrorStrategy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.TransientErrorStrategy.BackoffType.ShouldBe(BackoffType.DecorrelatedJitter);
		options.TransientErrorStrategy.JitterEnabled.ShouldBeFalse();
		options.TransientErrorStrategy.CircuitBreakerEnabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectResourceExhaustionStrategyDefaults()
	{
		// Arrange & Act
		var options = new PubSubRetryPolicyOptions();

		// Assert
		options.ResourceExhaustionStrategy.ShouldNotBeNull();
		options.ResourceExhaustionStrategy.MaxRetryAttempts.ShouldBe(3);
		options.ResourceExhaustionStrategy.InitialDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.ResourceExhaustionStrategy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.ResourceExhaustionStrategy.BackoffType.ShouldBe(BackoffType.Exponential);
		options.ResourceExhaustionStrategy.JitterEnabled.ShouldBeTrue();
		options.ResourceExhaustionStrategy.CircuitBreakerEnabled.ShouldBeTrue();
		options.ResourceExhaustionStrategy.CircuitBreakerThreshold.ShouldBe(2);
		options.ResourceExhaustionStrategy.CircuitBreakerDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveEmptyCustomStrategiesByDefault()
	{
		// Arrange & Act
		var options = new PubSubRetryPolicyOptions();

		// Assert
		options.CustomStrategies.ShouldBeEmpty();
		options.EnableAdaptiveRetries.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingCustomStrategies()
	{
		// Arrange
		var options = new PubSubRetryPolicyOptions();

		// Act
		options.CustomStrategies["custom-type"] = new RetryStrategy
		{
			MaxRetryAttempts = 10,
			BackoffType = BackoffType.Constant,
			InitialDelay = TimeSpan.FromSeconds(1),
		};

		// Assert
		options.CustomStrategies.Count.ShouldBe(1);
		options.CustomStrategies["custom-type"].MaxRetryAttempts.ShouldBe(10);
		options.CustomStrategies["custom-type"].BackoffType.ShouldBe(BackoffType.Constant);
	}
}
