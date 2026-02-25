// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Unit tests for <see cref="AdaptiveTtlMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AdaptiveTtl")]
public sealed class AdaptiveTtlMetricsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics();

		// Assert
		metrics.AverageAdjustmentFactor.ShouldBe(0);
		metrics.TotalCalculations.ShouldBe(0);
		metrics.TtlIncreases.ShouldBe(0);
		metrics.TtlDecreases.ShouldBe(0);
		metrics.AverageHitRate.ShouldBe(0);
		_ = metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.Count.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingAverageAdjustmentFactor()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			AverageAdjustmentFactor = 1.25
		};

		// Assert
		metrics.AverageAdjustmentFactor.ShouldBe(1.25);
	}

	[Fact]
	public void AllowSettingTotalCalculations()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			TotalCalculations = 10000
		};

		// Assert
		metrics.TotalCalculations.ShouldBe(10000);
	}

	[Fact]
	public void AllowSettingTtlIncreases()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			TtlIncreases = 6500
		};

		// Assert
		metrics.TtlIncreases.ShouldBe(6500);
	}

	[Fact]
	public void AllowSettingTtlDecreases()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			TtlDecreases = 3500
		};

		// Assert
		metrics.TtlDecreases.ShouldBe(3500);
	}

	[Fact]
	public void AllowSettingAverageHitRate()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			AverageHitRate = 0.87
		};

		// Assert
		metrics.AverageHitRate.ShouldBe(0.87);
	}

	[Fact]
	public void AllowAddingCustomMetrics()
	{
		// Arrange
		var metrics = new AdaptiveTtlMetrics();

		// Act
		metrics.CustomMetrics["p95_latency"] = 45.5;
		metrics.CustomMetrics["cache_evictions"] = 1234;

		// Assert
		metrics.CustomMetrics.Count.ShouldBe(2);
		metrics.CustomMetrics["p95_latency"].ShouldBe(45.5);
		metrics.CustomMetrics["cache_evictions"].ShouldBe(1234);
	}

	[Fact]
	public void CreateFullMetricsSnapshot()
	{
		// Act
		var metrics = new AdaptiveTtlMetrics
		{
			AverageAdjustmentFactor = 1.15,
			TotalCalculations = 50000,
			TtlIncreases = 30000,
			TtlDecreases = 20000,
			AverageHitRate = 0.92
		};
		metrics.CustomMetrics["memory_usage_mb"] = 256.5;
		metrics.CustomMetrics["active_keys"] = 10000;

		// Assert
		metrics.AverageAdjustmentFactor.ShouldBe(1.15);
		metrics.TotalCalculations.ShouldBe(50000);
		metrics.TtlIncreases.ShouldBe(30000);
		metrics.TtlDecreases.ShouldBe(20000);
		metrics.AverageHitRate.ShouldBe(0.92);
		metrics.CustomMetrics.Count.ShouldBe(2);
	}

	[Fact]
	public void TrackTotalIncreasesPlusDecreasesEqualsCalculations()
	{
		// Arrange
		var metrics = new AdaptiveTtlMetrics
		{
			TotalCalculations = 10000,
			TtlIncreases = 6500,
			TtlDecreases = 3500
		};

		// Assert
		(metrics.TtlIncreases + metrics.TtlDecreases).ShouldBe(metrics.TotalCalculations);
	}
}
