// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CachePerformanceSnapshot"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "Performance")]
public sealed class CachePerformanceSnapshotShould : UnitTestBase
{
	[Fact]
	public void HaveUtcNowTimestamp_ByDefault()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void HaveZeroHitCount_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroMissCount_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.MissCount.ShouldBe(0);
	}

	[Fact]
	public void CalculateCorrectHitRatio_WithPositiveRequests()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 80,
			MissCount = 20
		};

		// Assert
		snapshot.HitRatio.ShouldBe(0.8);
	}

	[Fact]
	public void ReturnZeroHitRatio_WhenNoRequests()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 0
		};

		// Assert
		snapshot.HitRatio.ShouldBe(0);
	}

	[Fact]
	public void CalculateTotalRequests_AsHitsPlusMisses()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 100,
			MissCount = 50
		};

		// Assert
		snapshot.TotalRequests.ShouldBe(150);
	}

	[Fact]
	public void CalculateAverageSizeBytes_WhenItemsExist()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ItemCount = 10,
			TotalSizeBytes = 1000
		};

		// Assert
		snapshot.AverageSizeBytes.ShouldBe(100);
	}

	[Fact]
	public void ReturnZeroAverageSizeBytes_WhenNoItems()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ItemCount = 0,
			TotalSizeBytes = 0
		};

		// Assert
		snapshot.AverageSizeBytes.ShouldBe(0);
	}

	[Fact]
	public void CalculateErrorRate_WithPositiveRequests()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 90,
			MissCount = 10,
			TotalErrors = 5
		};

		// Assert
		snapshot.ErrorRate.ShouldBe(0.05);
	}

	[Fact]
	public void ReturnZeroErrorRate_WhenNoRequests()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 0,
			TotalErrors = 0
		};

		// Assert
		snapshot.ErrorRate.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyErrorCounts_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.ErrorCounts.ShouldNotBeNull();
		snapshot.ErrorCounts.Count.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyCustomMetrics_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.CustomMetrics.ShouldNotBeNull();
		snapshot.CustomMetrics.Count.ShouldBe(0);
	}

	[Fact]
	public void HaveIsHealthyTrue_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyHealthWarnings_ByDefault()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.HealthWarnings.ShouldNotBeNull();
		snapshot.HealthWarnings.Count.ShouldBe(0);
	}

	[Fact]
	public void ToStringIncludesHitRatio()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 80,
			MissCount = 20
		};

		// Act
		var result = snapshot.ToString();

		// Assert
		result.ShouldContain("HitRatio");
	}

	[Fact]
	public void ToStringIncludesItemCount()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ItemCount = 500
		};

		// Act
		var result = snapshot.ToString();

		// Assert
		result.ShouldContain("Items=");
	}

	[Fact]
	public void ToStringIncludesThroughput()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ThroughputOpsPerSecond = 1000
		};

		// Act
		var result = snapshot.ToString();

		// Assert
		result.ShouldContain("Throughput=");
	}

	[Fact]
	public void InitializeWithAllMetrics()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 800,
			MissCount = 200,
			ItemCount = 100,
			TotalSizeBytes = 1024 * 1024,
			EvictionCount = 50,
			AverageGetTimeMs = 5.5,
			AverageSetTimeMs = 10.2,
			P95GetTimeMs = 15.0,
			P99GetTimeMs = 25.0,
			MaxGetTimeMs = 100.0,
			MemoryPressure = 30,
			CpuUsagePercent = 15.5,
			ConcurrentOperations = 10,
			PendingOperations = 5,
			ThroughputOpsPerSecond = 500.0,
			TotalErrors = 3,
			CollectionDuration = TimeSpan.FromMinutes(5),
			IsHealthy = true
		};

		// Assert
		snapshot.HitCount.ShouldBe(800);
		snapshot.MissCount.ShouldBe(200);
		snapshot.ItemCount.ShouldBe(100);
		snapshot.TotalSizeBytes.ShouldBe(1024 * 1024);
		snapshot.EvictionCount.ShouldBe(50);
		snapshot.AverageGetTimeMs.ShouldBe(5.5);
		snapshot.AverageSetTimeMs.ShouldBe(10.2);
		snapshot.P95GetTimeMs.ShouldBe(15.0);
		snapshot.P99GetTimeMs.ShouldBe(25.0);
		snapshot.MaxGetTimeMs.ShouldBe(100.0);
		snapshot.MemoryPressure.ShouldBe(30);
		snapshot.CpuUsagePercent.ShouldBe(15.5);
		snapshot.ConcurrentOperations.ShouldBe(10);
		snapshot.PendingOperations.ShouldBe(5);
		snapshot.ThroughputOpsPerSecond.ShouldBe(500.0);
		snapshot.TotalErrors.ShouldBe(3);
		snapshot.CollectionDuration.ShouldBe(TimeSpan.FromMinutes(5));
		snapshot.IsHealthy.ShouldBeTrue();
	}
}
