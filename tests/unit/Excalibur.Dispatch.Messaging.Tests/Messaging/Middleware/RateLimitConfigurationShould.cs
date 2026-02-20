// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="RateLimitConfiguration"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RateLimitConfigurationShould
{
	[Fact]
	public void HaveDefaultAlgorithmOfTokenBucket()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
	}

	[Fact]
	public void HaveDefaultTokenLimitOfOneHundred()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.TokenLimit.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultReplenishmentPeriodOfOneSecond()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.ReplenishmentPeriod.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultTokensPerPeriodOfOneHundred()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.TokensPerPeriod.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultPermitLimitOfOneHundred()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.PermitLimit.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultWindowOfOneMinute()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.Window.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveDefaultSegmentsPerWindowOfFour()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration();

		// Assert
		config.SegmentsPerWindow.ShouldBe(4);
	}

	[Theory]
	[InlineData(RateLimitAlgorithm.TokenBucket)]
	[InlineData(RateLimitAlgorithm.SlidingWindow)]
	[InlineData(RateLimitAlgorithm.FixedWindow)]
	[InlineData(RateLimitAlgorithm.Concurrency)]
	public void AllowSettingAlgorithm(RateLimitAlgorithm algorithm)
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.Algorithm = algorithm;

		// Assert
		config.Algorithm.ShouldBe(algorithm);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public void AllowSettingTokenLimit(int tokenLimit)
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.TokenLimit = tokenLimit;

		// Assert
		config.TokenLimit.ShouldBe(tokenLimit);
	}

	[Fact]
	public void AllowSettingReplenishmentPeriod()
	{
		// Arrange
		var config = new RateLimitConfiguration();
		var period = TimeSpan.FromSeconds(5);

		// Act
		config.ReplenishmentPeriod = period;

		// Assert
		config.ReplenishmentPeriod.ShouldBe(period);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(500)]
	public void AllowSettingTokensPerPeriod(int tokensPerPeriod)
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.TokensPerPeriod = tokensPerPeriod;

		// Assert
		config.TokensPerPeriod.ShouldBe(tokensPerPeriod);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AllowSettingPermitLimit(int permitLimit)
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.PermitLimit = permitLimit;

		// Assert
		config.PermitLimit.ShouldBe(permitLimit);
	}

	[Fact]
	public void AllowSettingWindow()
	{
		// Arrange
		var config = new RateLimitConfiguration();
		var window = TimeSpan.FromMinutes(5);

		// Act
		config.Window = window;

		// Assert
		config.Window.ShouldBe(window);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(8)]
	[InlineData(10)]
	[InlineData(60)]
	public void AllowSettingSegmentsPerWindow(int segments)
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.SegmentsPerWindow = segments;

		// Assert
		config.SegmentsPerWindow.ShouldBe(segments);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var config = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.SlidingWindow,
			TokenLimit = 500,
			ReplenishmentPeriod = TimeSpan.FromSeconds(10),
			TokensPerPeriod = 50,
			PermitLimit = 200,
			Window = TimeSpan.FromMinutes(5),
			SegmentsPerWindow = 10,
		};

		// Assert
		config.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
		config.TokenLimit.ShouldBe(500);
		config.ReplenishmentPeriod.ShouldBe(TimeSpan.FromSeconds(10));
		config.TokensPerPeriod.ShouldBe(50);
		config.PermitLimit.ShouldBe(200);
		config.Window.ShouldBe(TimeSpan.FromMinutes(5));
		config.SegmentsPerWindow.ShouldBe(10);
	}

	[Fact]
	public void AllowZeroTokenLimit()
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.TokenLimit = 0;

		// Assert
		config.TokenLimit.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroWindow()
	{
		// Arrange
		var config = new RateLimitConfiguration();

		// Act
		config.Window = TimeSpan.Zero;

		// Assert
		config.Window.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void SimulateTypicalHighThroughputConfiguration()
	{
		// Arrange & Act - High throughput API configuration
		var config = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.TokenBucket,
			TokenLimit = 10000,
			ReplenishmentPeriod = TimeSpan.FromSeconds(1),
			TokensPerPeriod = 1000,
		};

		// Assert
		config.TokenLimit.ShouldBe(10000);
		config.TokensPerPeriod.ShouldBe(1000);
	}

	[Fact]
	public void SimulateTypicalLowThroughputConfiguration()
	{
		// Arrange & Act - Conservative rate limit for expensive operations
		var config = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.FixedWindow,
			PermitLimit = 10,
			Window = TimeSpan.FromHours(1),
		};

		// Assert
		config.PermitLimit.ShouldBe(10);
		config.Window.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void SimulateTypicalSlidingWindowConfiguration()
	{
		// Arrange & Act - Sliding window for smoother rate limiting
		var config = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.SlidingWindow,
			PermitLimit = 100,
			Window = TimeSpan.FromMinutes(1),
			SegmentsPerWindow = 6, // 10-second segments
		};

		// Assert
		config.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
		config.SegmentsPerWindow.ShouldBe(6);
	}
}
