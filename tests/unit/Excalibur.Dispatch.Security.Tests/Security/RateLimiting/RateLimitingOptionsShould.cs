// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitingOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitingOptionsShould
{
	[Fact]
	public void HaveTrueEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveTokenBucketAlgorithm_ByDefault()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
	}

	[Fact]
	public void HaveDefaultLimitsInitialized()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultLimits.ShouldNotBeNull();
	}

	[Fact]
	public void HaveEmptyTenantLimits_ByDefault()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.TenantLimits.ShouldNotBeNull();
		options.TenantLimits.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyTierLimits_ByDefault()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.TierLimits.ShouldNotBeNull();
		options.TierLimits.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultRetryAfterOf1000Milliseconds()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultRetryAfterMilliseconds.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultCleanupIntervalOf5Minutes()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.CleanupIntervalMinutes.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultInactivityTimeoutOf30Minutes()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.InactivityTimeoutMinutes.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData(RateLimitAlgorithm.Unknown)]
	[InlineData(RateLimitAlgorithm.TokenBucket)]
	[InlineData(RateLimitAlgorithm.SlidingWindow)]
	[InlineData(RateLimitAlgorithm.FixedWindow)]
	[InlineData(RateLimitAlgorithm.Concurrency)]
	public void AllowSettingAlgorithm(RateLimitAlgorithm algorithm)
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.Algorithm = algorithm;

		// Assert
		options.Algorithm.ShouldBe(algorithm);
	}

	[Fact]
	public void AllowSettingDefaultLimits()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var limits = new RateLimits { TokenLimit = 500 };

		// Act
		options.DefaultLimits = limits;

		// Assert
		options.DefaultLimits.ShouldBe(limits);
	}

	[Fact]
	public void AllowAddingTenantLimits()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var premiumLimits = new RateLimits { TokenLimit = 1000 };

		// Act
		options.TenantLimits["tenant-premium"] = premiumLimits;

		// Assert
		options.TenantLimits.Count.ShouldBe(1);
		options.TenantLimits["tenant-premium"].TokenLimit.ShouldBe(1000);
	}

	[Fact]
	public void AllowAddingTierLimits()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var freeLimits = new RateLimits { TokenLimit = 50 };
		var enterpriseLimits = new RateLimits { TokenLimit = 10000 };

		// Act
		options.TierLimits["free"] = freeLimits;
		options.TierLimits["enterprise"] = enterpriseLimits;

		// Assert
		options.TierLimits.Count.ShouldBe(2);
		options.TierLimits["free"].TokenLimit.ShouldBe(50);
		options.TierLimits["enterprise"].TokenLimit.ShouldBe(10000);
	}

	[Fact]
	public void AllowSettingDefaultRetryAfterMilliseconds()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.DefaultRetryAfterMilliseconds = 5000;

		// Assert
		options.DefaultRetryAfterMilliseconds.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingCleanupIntervalMinutes()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.CleanupIntervalMinutes = 10;

		// Assert
		options.CleanupIntervalMinutes.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingInactivityTimeoutMinutes()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.InactivityTimeoutMinutes = 60;

		// Assert
		options.InactivityTimeoutMinutes.ShouldBe(60);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var defaultLimits = new RateLimits { TokenLimit = 500 };

		// Act
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.SlidingWindow,
			DefaultLimits = defaultLimits,
			DefaultRetryAfterMilliseconds = 2000,
			CleanupIntervalMinutes = 15,
			InactivityTimeoutMinutes = 45,
		};
		options.TenantLimits["premium"] = new RateLimits { TokenLimit = 2000 };
		options.TierLimits["gold"] = new RateLimits { TokenLimit = 3000 };

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
		options.DefaultLimits.TokenLimit.ShouldBe(500);
		options.TenantLimits["premium"].TokenLimit.ShouldBe(2000);
		options.TierLimits["gold"].TokenLimit.ShouldBe(3000);
		options.DefaultRetryAfterMilliseconds.ShouldBe(2000);
		options.CleanupIntervalMinutes.ShouldBe(15);
		options.InactivityTimeoutMinutes.ShouldBe(45);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(RateLimitingOptions).IsSealed.ShouldBeTrue();
	}
}
