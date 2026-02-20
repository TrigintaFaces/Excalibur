// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="RetryPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryPolicyShould
{
	[Fact]
	public void HaveDefaultMaxAttemptsOfThree()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultInitialDelayOfOneSecond()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultMaxDelayOfOneMinute()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveDefaultBackoffMultiplierOfTwo()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.BackoffMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void HaveUseExponentialBackoffEnabledByDefault()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(100)]
	public void AllowSettingMaxAttempts(int maxAttempts)
	{
		// Arrange
		var policy = new RetryPolicy();

		// Act
		policy.MaxAttempts = maxAttempts;

		// Assert
		policy.MaxAttempts.ShouldBe(maxAttempts);
	}

	[Fact]
	public void AllowSettingInitialDelay()
	{
		// Arrange
		var policy = new RetryPolicy();
		var delay = TimeSpan.FromMilliseconds(500);

		// Act
		policy.InitialDelay = delay;

		// Assert
		policy.InitialDelay.ShouldBe(delay);
	}

	[Fact]
	public void AllowSettingMaxDelay()
	{
		// Arrange
		var policy = new RetryPolicy();
		var maxDelay = TimeSpan.FromMinutes(5);

		// Act
		policy.MaxDelay = maxDelay;

		// Assert
		policy.MaxDelay.ShouldBe(maxDelay);
	}

	[Theory]
	[InlineData(1.0)]
	[InlineData(1.5)]
	[InlineData(2.0)]
	[InlineData(3.0)]
	[InlineData(10.0)]
	public void AllowSettingBackoffMultiplier(double multiplier)
	{
		// Arrange
		var policy = new RetryPolicy();

		// Act
		policy.BackoffMultiplier = multiplier;

		// Assert
		policy.BackoffMultiplier.ShouldBe(multiplier);
	}

	[Fact]
	public void AllowSettingUseExponentialBackoff()
	{
		// Arrange
		var policy = new RetryPolicy();

		// Act
		policy.UseExponentialBackoff = false;

		// Assert
		policy.UseExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var policy = new RetryPolicy
		{
			MaxAttempts = 5,
			InitialDelay = TimeSpan.FromMilliseconds(250),
			MaxDelay = TimeSpan.FromSeconds(30),
			BackoffMultiplier = 1.5,
			UseExponentialBackoff = true,
		};

		// Assert
		policy.MaxAttempts.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		policy.BackoffMultiplier.ShouldBe(1.5);
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void AllowZeroInitialDelay()
	{
		// Arrange
		var policy = new RetryPolicy();

		// Act
		policy.InitialDelay = TimeSpan.Zero;

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowZeroMaxDelay()
	{
		// Arrange
		var policy = new RetryPolicy();

		// Act
		policy.MaxDelay = TimeSpan.Zero;

		// Assert
		policy.MaxDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowBackoffMultiplierOfOne()
	{
		// Arrange - Multiplier of 1 means no backoff increase
		var policy = new RetryPolicy();

		// Act
		policy.BackoffMultiplier = 1.0;

		// Assert
		policy.BackoffMultiplier.ShouldBe(1.0);
	}

	[Fact]
	public void AllowBackoffMultiplierLessThanOne()
	{
		// Arrange - Values less than 1 would decrease delay (unusual but allowed)
		var policy = new RetryPolicy();

		// Act
		policy.BackoffMultiplier = 0.5;

		// Assert
		policy.BackoffMultiplier.ShouldBe(0.5);
	}

	[Fact]
	public void SimulateTypicalAggressiveRetryPolicy()
	{
		// Arrange & Act - Aggressive retry for critical operations
		var policy = new RetryPolicy
		{
			MaxAttempts = 10,
			InitialDelay = TimeSpan.FromMilliseconds(100),
			MaxDelay = TimeSpan.FromSeconds(10),
			BackoffMultiplier = 2.0,
			UseExponentialBackoff = true,
		};

		// Assert
		policy.MaxAttempts.ShouldBe(10);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void SimulateTypicalConservativeRetryPolicy()
	{
		// Arrange & Act - Conservative retry to avoid overloading
		var policy = new RetryPolicy
		{
			MaxAttempts = 2,
			InitialDelay = TimeSpan.FromSeconds(5),
			MaxDelay = TimeSpan.FromMinutes(2),
			BackoffMultiplier = 3.0,
			UseExponentialBackoff = true,
		};

		// Assert
		policy.MaxAttempts.ShouldBe(2);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
		policy.BackoffMultiplier.ShouldBe(3.0);
	}

	[Fact]
	public void SimulateTypicalFixedDelayRetryPolicy()
	{
		// Arrange & Act - Fixed delay (no exponential backoff)
		var policy = new RetryPolicy
		{
			MaxAttempts = 5,
			InitialDelay = TimeSpan.FromSeconds(1),
			UseExponentialBackoff = false,
		};

		// Assert
		policy.UseExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void SimulateTypicalNoRetryPolicy()
	{
		// Arrange & Act - No retries (immediate failure)
		var policy = new RetryPolicy
		{
			MaxAttempts = 0,
		};

		// Assert
		policy.MaxAttempts.ShouldBe(0);
	}
}
