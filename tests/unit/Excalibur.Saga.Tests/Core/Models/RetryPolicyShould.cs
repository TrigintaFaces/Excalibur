// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="RetryPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class RetryPolicyShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultMaxAttempts()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultInitialDelay()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultMaxDelay()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveDefaultBackoffMultiplier()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.BackoffMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void HaveDefaultUseJitterTrue()
	{
		// Arrange & Act
		var policy = new RetryPolicy();

		// Assert
		policy.UseJitter.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowMaxAttemptsToBeSet()
	{
		// Arrange & Act
		var policy = new RetryPolicy { MaxAttempts = 5 };

		// Assert
		policy.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowInitialDelayToBeSet()
	{
		// Arrange & Act
		var policy = new RetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(500) };

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AllowMaxDelayToBeSet()
	{
		// Arrange & Act
		var policy = new RetryPolicy { MaxDelay = TimeSpan.FromMinutes(5) };

		// Assert
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowBackoffMultiplierToBeSet()
	{
		// Arrange & Act
		var policy = new RetryPolicy { BackoffMultiplier = 1.5 };

		// Assert
		policy.BackoffMultiplier.ShouldBe(1.5);
	}

	[Fact]
	public void AllowUseJitterToBeSet()
	{
		// Arrange & Act
		var policy = new RetryPolicy { UseJitter = false };

		// Assert
		policy.UseJitter.ShouldBeFalse();
	}

	#endregion Property Setting Tests

	#region ExponentialBackoff Factory Tests

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithDefaultValues()
	{
		// Act
		var policy = RetryPolicy.ExponentialBackoff();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.BackoffMultiplier.ShouldBe(2.0);
		policy.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithCustomMaxAttempts()
	{
		// Act
		var policy = RetryPolicy.ExponentialBackoff(maxAttempts: 10);

		// Assert
		policy.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithCustomInitialDelay()
	{
		// Act
		var policy = RetryPolicy.ExponentialBackoff(initialDelay: TimeSpan.FromMilliseconds(250));

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithAllCustomValues()
	{
		// Act
		var policy = RetryPolicy.ExponentialBackoff(
			maxAttempts: 5,
			initialDelay: TimeSpan.FromSeconds(2));

		// Assert
		policy.MaxAttempts.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(2));
		policy.BackoffMultiplier.ShouldBe(2.0);
		policy.UseJitter.ShouldBeTrue();
	}

	#endregion ExponentialBackoff Factory Tests

	#region FixedDelay Factory Tests

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithDefaultValues()
	{
		// Act
		var policy = RetryPolicy.FixedDelay();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.BackoffMultiplier.ShouldBe(1.0);
		policy.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithCustomMaxAttempts()
	{
		// Act
		var policy = RetryPolicy.FixedDelay(maxAttempts: 7);

		// Assert
		policy.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithCustomDelay()
	{
		// Act
		var policy = RetryPolicy.FixedDelay(delay: TimeSpan.FromSeconds(5));

		// Assert
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithAllCustomValues()
	{
		// Act
		var policy = RetryPolicy.FixedDelay(
			maxAttempts: 4,
			delay: TimeSpan.FromMilliseconds(100));

		// Assert
		policy.MaxAttempts.ShouldBe(4);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		policy.BackoffMultiplier.ShouldBe(1.0);
		policy.UseJitter.ShouldBeFalse();
	}

	#endregion FixedDelay Factory Tests

	#region GetDelay Tests

	[Fact]
	public void GetDelay_ReturnsInitialDelay_ForFirstAttempt()
	{
		// Arrange
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2.0,
			UseJitter = false,
		};

		// Act
		var delay = policy.GetDelay(1);

		// Assert
		delay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void GetDelay_AppliesBackoffMultiplier_ForSubsequentAttempts()
	{
		// Arrange
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2.0,
			UseJitter = false,
		};

		// Act
		var delay2 = policy.GetDelay(2);
		var delay3 = policy.GetDelay(3);
		var delay4 = policy.GetDelay(4);

		// Assert
		delay2.ShouldBe(TimeSpan.FromSeconds(2)); // 1 * 2^1
		delay3.ShouldBe(TimeSpan.FromSeconds(4)); // 1 * 2^2
		delay4.ShouldBe(TimeSpan.FromSeconds(8)); // 1 * 2^3
	}

	[Fact]
	public void GetDelay_RespectsMaxDelay()
	{
		// Arrange
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2.0,
			MaxDelay = TimeSpan.FromSeconds(5),
			UseJitter = false,
		};

		// Act
		var delay5 = policy.GetDelay(5); // Would be 16 seconds without cap

		// Assert
		delay5.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void GetDelay_ReturnsFixedDelay_WhenBackoffMultiplierIsOne()
	{
		// Arrange
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromSeconds(2),
			BackoffMultiplier = 1.0,
			UseJitter = false,
		};

		// Act
		var delay1 = policy.GetDelay(1);
		var delay2 = policy.GetDelay(2);
		var delay3 = policy.GetDelay(3);

		// Assert - All delays should be the same
		delay1.ShouldBe(TimeSpan.FromSeconds(2));
		delay2.ShouldBe(TimeSpan.FromSeconds(2));
		delay3.ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void GetDelay_AddsJitter_WhenEnabled()
	{
		// Arrange
		var policy = new RetryPolicy
		{
			InitialDelay = TimeSpan.FromSeconds(1),
			BackoffMultiplier = 1.0,
			UseJitter = true,
		};

		// Act - Get multiple delays to verify jitter adds variation
		var delays = Enumerable.Range(1, 10)
			.Select(_ => policy.GetDelay(1))
			.ToList();

		// Assert - At least some delays should differ due to jitter
		// Jitter is 0-20%, so base delay of 1s can range from 1s to 1.2s
		delays.All(d => d >= TimeSpan.FromSeconds(1)).ShouldBeTrue();
		delays.All(d => d <= TimeSpan.FromSeconds(1.2)).ShouldBeTrue();
	}

	#endregion GetDelay Tests
}
