// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Unit tests for <see cref="AdaptiveTtlOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AdaptiveTtl")]
public sealed class AdaptiveTtlOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultMinTtl()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultMaxTtl()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void HaveDefaultTargetHitRate()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.TargetHitRate.ShouldBe(0.8);
	}

	[Fact]
	public void HaveDefaultTargetResponseTime()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.TargetResponseTime.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void HaveDefaultLearningRate()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.LearningRate.ShouldBe(0.1);
	}

	[Fact]
	public void HaveDefaultDiscountFactor()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.DiscountFactor.ShouldBe(0.9);
	}

	[Fact]
	public void HaveDefaultWeights()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Weights.HitRateWeight.ShouldBe(0.3);
		options.Weights.AccessFrequencyWeight.ShouldBe(0.25);
		options.Weights.TemporalWeight.ShouldBe(0.15);
		options.Weights.CostWeight.ShouldBe(0.15);
		options.Weights.LoadWeight.ShouldBe(0.1);
		options.Weights.VolatilityWeight.ShouldBe(0.05);
	}

	[Fact]
	public void HaveDefaultLoadThresholds()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Thresholds.HighLoadThreshold.ShouldBe(0.8);
		options.Thresholds.LowLoadThreshold.ShouldBe(0.3);
	}

	[Fact]
	public void HaveDefaultMaxExpectedFrequency()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Thresholds.MaxExpectedFrequency.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultMaxExpectedMissCostMs()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Thresholds.MaxExpectedMissCostMs.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultLargeContentThresholdMb()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Thresholds.LargeContentThresholdMb.ShouldBe(10);
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Act
		var options = new AdaptiveTtlOptions
		{
			MinTtl = TimeSpan.FromSeconds(10),
			MaxTtl = TimeSpan.FromHours(12),
			TargetHitRate = 0.9,
			TargetResponseTime = TimeSpan.FromMilliseconds(25),
			LearningRate = 0.2,
			DiscountFactor = 0.85,
			Thresholds = { HighLoadThreshold = 0.9, LowLoadThreshold = 0.2, MaxExpectedFrequency = 500, MaxExpectedMissCostMs = 2000, LargeContentThresholdMb = 20 },
		};

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(12));
		options.TargetHitRate.ShouldBe(0.9);
		options.TargetResponseTime.ShouldBe(TimeSpan.FromMilliseconds(25));
		options.LearningRate.ShouldBe(0.2);
		options.DiscountFactor.ShouldBe(0.85);
		options.Thresholds.HighLoadThreshold.ShouldBe(0.9);
		options.Thresholds.LowLoadThreshold.ShouldBe(0.2);
		options.Thresholds.MaxExpectedFrequency.ShouldBe(500);
		options.Thresholds.MaxExpectedMissCostMs.ShouldBe(2000);
		options.Thresholds.LargeContentThresholdMb.ShouldBe(20);
	}

	[Fact]
	public void HaveWeightsSumToApproximatelyOne()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		var totalWeight = options.Weights.HitRateWeight +
			options.Weights.AccessFrequencyWeight +
			options.Weights.TemporalWeight +
			options.Weights.CostWeight +
			options.Weights.LoadWeight +
			options.Weights.VolatilityWeight;

		// Assert
		totalWeight.ShouldBe(1.0, 0.001);
	}
}
