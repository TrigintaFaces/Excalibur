// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimits"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitsShould
{
	[Fact]
	public void HaveDefaultTokenLimitOf100()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.TokenLimit.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultTokensPerPeriodOf20()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.TokensPerPeriod.ShouldBe(20);
	}

	[Fact]
	public void HaveDefaultReplenishmentPeriodSecondsOf1()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.ReplenishmentPeriodSeconds.ShouldBe(1);
	}

	[Fact]
	public void HaveDefaultPermitLimitOf100()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.PermitLimit.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultWindowSecondsOf60()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.WindowSeconds.ShouldBe(60);
	}

	[Fact]
	public void HaveDefaultSegmentsPerWindowOf4()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.SegmentsPerWindow.ShouldBe(4);
	}

	[Fact]
	public void HaveDefaultConcurrencyLimitOf10()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.ConcurrencyLimit.ShouldBe(10);
	}

	[Fact]
	public void HaveDefaultQueueLimitOf10()
	{
		// Arrange & Act
		var limits = new RateLimits();

		// Assert
		limits.QueueLimit.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingTokenLimit()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.TokenLimit = 500;

		// Assert
		limits.TokenLimit.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingTokensPerPeriod()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.TokensPerPeriod = 50;

		// Assert
		limits.TokensPerPeriod.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingReplenishmentPeriodSeconds()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.ReplenishmentPeriodSeconds = 5;

		// Assert
		limits.ReplenishmentPeriodSeconds.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingPermitLimit()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.PermitLimit = 200;

		// Assert
		limits.PermitLimit.ShouldBe(200);
	}

	[Fact]
	public void AllowSettingWindowSeconds()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.WindowSeconds = 120;

		// Assert
		limits.WindowSeconds.ShouldBe(120);
	}

	[Fact]
	public void AllowSettingSegmentsPerWindow()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.SegmentsPerWindow = 8;

		// Assert
		limits.SegmentsPerWindow.ShouldBe(8);
	}

	[Fact]
	public void AllowSettingConcurrencyLimit()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.ConcurrencyLimit = 50;

		// Assert
		limits.ConcurrencyLimit.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingQueueLimit()
	{
		// Arrange
		var limits = new RateLimits();

		// Act
		limits.QueueLimit = 100;

		// Assert
		limits.QueueLimit.ShouldBe(100);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var limits = new RateLimits
		{
			TokenLimit = 1000,
			TokensPerPeriod = 100,
			ReplenishmentPeriodSeconds = 10,
			PermitLimit = 500,
			WindowSeconds = 300,
			SegmentsPerWindow = 12,
			ConcurrencyLimit = 100,
			QueueLimit = 50,
		};

		// Assert
		limits.TokenLimit.ShouldBe(1000);
		limits.TokensPerPeriod.ShouldBe(100);
		limits.ReplenishmentPeriodSeconds.ShouldBe(10);
		limits.PermitLimit.ShouldBe(500);
		limits.WindowSeconds.ShouldBe(300);
		limits.SegmentsPerWindow.ShouldBe(12);
		limits.ConcurrencyLimit.ShouldBe(100);
		limits.QueueLimit.ShouldBe(50);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(RateLimits).IsSealed.ShouldBeTrue();
	}
}
