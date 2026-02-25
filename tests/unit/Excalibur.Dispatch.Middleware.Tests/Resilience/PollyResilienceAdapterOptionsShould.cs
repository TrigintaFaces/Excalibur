// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using PollyRetryOptions = Excalibur.Dispatch.Resilience.Polly.RetryOptions;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyResilienceAdapterOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyResilienceAdapterOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Act
		var options = new PollyResilienceAdapterOptions();

		// Assert
		options.RetryOptions.ShouldBeNull();
		options.CircuitBreakerOptions.ShouldBeNull();
		options.MaxBackoffDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRetryOptions()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions();
		var retryOptions = new PollyRetryOptions
		{
			MaxRetries = 5,
			BaseDelay = TimeSpan.FromMilliseconds(100)
		};

		// Act
		options.RetryOptions = retryOptions;

		// Assert
		options.RetryOptions.ShouldNotBeNull();
		options.RetryOptions.MaxRetries.ShouldBe(5);
		options.RetryOptions.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void AllowSettingCircuitBreakerOptions()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions();
		var cbOptions = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			OpenDuration = TimeSpan.FromMinutes(1)
		};

		// Act
		options.CircuitBreakerOptions = cbOptions;

		// Assert
		options.CircuitBreakerOptions.ShouldNotBeNull();
		options.CircuitBreakerOptions.FailureThreshold.ShouldBe(10);
		options.CircuitBreakerOptions.OpenDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowSettingMaxBackoffDelay()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions();

		// Act
		options.MaxBackoffDelay = TimeSpan.FromMinutes(2);

		// Assert
		options.MaxBackoffDelay.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowSettingEnableTelemetry()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions();

		// Act
		options.EnableTelemetry = true;

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingRetryOptionsToNull()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions
		{
			RetryOptions = new PollyRetryOptions()
		};

		// Act
		options.RetryOptions = null;

		// Assert
		options.RetryOptions.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCircuitBreakerOptionsToNull()
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions
		{
			CircuitBreakerOptions = new CircuitBreakerOptions()
		};

		// Act
		options.CircuitBreakerOptions = null;

		// Assert
		options.CircuitBreakerOptions.ShouldBeNull();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(300)]
	public void AcceptVariousMaxBackoffDelayValues(int seconds)
	{
		// Arrange
		var options = new PollyResilienceAdapterOptions();

		// Act
		options.MaxBackoffDelay = TimeSpan.FromSeconds(seconds);

		// Assert
		options.MaxBackoffDelay.ShouldBe(TimeSpan.FromSeconds(seconds));
	}

	[Fact]
	public void AllowCompleteConfiguration()
	{
		// Arrange & Act
		var options = new PollyResilienceAdapterOptions
		{
			RetryOptions = new PollyRetryOptions
			{
				MaxRetries = 5,
				BaseDelay = TimeSpan.FromMilliseconds(100),
				BackoffStrategy = BackoffStrategy.Exponential,
				UseJitter = true
			},
			CircuitBreakerOptions = new CircuitBreakerOptions
			{
				FailureThreshold = 5,
				OpenDuration = TimeSpan.FromSeconds(30)
			},
			MaxBackoffDelay = TimeSpan.FromMinutes(1),
			EnableTelemetry = true
		};

		// Assert
		options.RetryOptions.ShouldNotBeNull();
		options.RetryOptions.MaxRetries.ShouldBe(5);
		options.RetryOptions.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
		options.RetryOptions.UseJitter.ShouldBeTrue();

		options.CircuitBreakerOptions.ShouldNotBeNull();
		options.CircuitBreakerOptions.FailureThreshold.ShouldBe(5);

		options.MaxBackoffDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableTelemetry.ShouldBeTrue();
	}
}
