// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CachePerformanceSnapshot functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CachePerformanceSnapshotShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedInitialValues()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(0);
		snapshot.TotalRequests.ShouldBe(0);
		snapshot.HitRatio.ShouldBe(0);
		snapshot.ItemCount.ShouldBe(0);
		snapshot.TotalSizeBytes.ShouldBe(0);
		snapshot.AverageSizeBytes.ShouldBe(0);
		snapshot.EvictionCount.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(0);
		snapshot.ErrorRate.ShouldBe(0);
		snapshot.IsHealthy.ShouldBeTrue();
		snapshot.ErrorCounts.ShouldBeEmpty();
		snapshot.HealthWarnings.ShouldBeEmpty();
		snapshot.CustomMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void HitRatio_WithNoRequests_ReturnsZero()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 0
		};

		// Act
		var hitRatio = snapshot.HitRatio;

		// Assert
		hitRatio.ShouldBe(0);
	}

	[Fact]
	public void HitRatio_WithAllHits_ReturnsOne()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 100,
			MissCount = 0
		};

		// Act
		var hitRatio = snapshot.HitRatio;

		// Assert
		hitRatio.ShouldBe(1.0);
	}

	[Fact]
	public void HitRatio_WithAllMisses_ReturnsZero()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 100
		};

		// Act
		var hitRatio = snapshot.HitRatio;

		// Assert
		hitRatio.ShouldBe(0);
	}

	[Fact]
	public void HitRatio_WithMixedResults_ReturnsCorrectRatio()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 75,
			MissCount = 25
		};

		// Act
		var hitRatio = snapshot.HitRatio;

		// Assert
		hitRatio.ShouldBe(0.75);
	}

	[Fact]
	public void TotalRequests_SumsHitsAndMisses()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 42,
			MissCount = 58
		};

		// Act
		var totalRequests = snapshot.TotalRequests;

		// Assert
		totalRequests.ShouldBe(100);
	}

	[Fact]
	public void AverageSizeBytes_WithNoItems_ReturnsZero()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ItemCount = 0,
			TotalSizeBytes = 0
		};

		// Act
		var avgSize = snapshot.AverageSizeBytes;

		// Assert
		avgSize.ShouldBe(0);
	}

	[Fact]
	public void AverageSizeBytes_WithItems_ReturnsCorrectAverage()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			ItemCount = 50,
			TotalSizeBytes = 5000
		};

		// Act
		var avgSize = snapshot.AverageSizeBytes;

		// Assert
		avgSize.ShouldBe(100);
	}

	[Fact]
	public void ErrorRate_WithNoRequests_ReturnsZero()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 0,
			TotalErrors = 0
		};

		// Act
		var errorRate = snapshot.ErrorRate;

		// Assert
		errorRate.ShouldBe(0);
	}

	[Fact]
	public void ErrorRate_WithErrors_ReturnsCorrectRate()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 95,
			MissCount = 5,
			TotalErrors = 5
		};

		// Act
		var errorRate = snapshot.ErrorRate;

		// Assert
		errorRate.ShouldBe(0.05);
	}

	[Fact]
	public void ErrorRate_WithAllErrors_ReturnsOne()
	{
		// Arrange
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 0,
			MissCount = 100,
			TotalErrors = 100
		};

		// Act
		var errorRate = snapshot.ErrorRate;

		// Assert
		errorRate.ShouldBe(1.0);
	}

	[Fact]
	public void ToString_FormatsCorrectly()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
		var snapshot = new CachePerformanceSnapshot
		{
			Timestamp = timestamp,
			HitCount = 80,
			MissCount = 20,
			ItemCount = 500,
			TotalSizeBytes = 10485760, // 10 MB
			AverageGetTimeMs = 2.5,
			ThroughputOpsPerSecond = 1000.5,
			TotalErrors = 1
		};

		// Act
		var result = snapshot.ToString();

		// Assert
		result.ShouldContain("CachePerformanceSnapshot");
		result.ShouldContain("2026-02-01T12:00:00");
		result.ShouldContain("HitRatio=80");
		result.ShouldContain("Items=500");
		result.ShouldContain("Size=10.00MB");
		result.ShouldContain("AvgGetTime=2.50ms");
		result.ShouldContain("Throughput=1000.5ops/s");
		result.ShouldContain("Errors=1.00%");
	}

	[Fact]
	public void Create_WithCustomMetrics_StoresCorrectly()
	{
		// Arrange
		var customMetrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CustomKey1"] = "CustomValue1",
			["CustomKey2"] = 42
		};

		// Act
		var snapshot = new CachePerformanceSnapshot
		{
			CustomMetrics = customMetrics
		};

		// Assert
		snapshot.CustomMetrics.ShouldContainKey("CustomKey1");
		snapshot.CustomMetrics["CustomKey1"].ShouldBe("CustomValue1");
		snapshot.CustomMetrics.ShouldContainKey("CustomKey2");
		snapshot.CustomMetrics["CustomKey2"].ShouldBe(42);
	}

	[Fact]
	public void Create_WithErrorCounts_StoresCorrectly()
	{
		// Arrange
		var errorCounts = new Dictionary<string, long>(StringComparer.Ordinal)
		{
			["TimeoutError"] = 5,
			["NetworkError"] = 3
		};

		// Act
		var snapshot = new CachePerformanceSnapshot
		{
			ErrorCounts = errorCounts,
			TotalErrors = 8
		};

		// Assert
		snapshot.ErrorCounts.ShouldContainKey("TimeoutError");
		snapshot.ErrorCounts["TimeoutError"].ShouldBe(5);
		snapshot.ErrorCounts.ShouldContainKey("NetworkError");
		snapshot.ErrorCounts["NetworkError"].ShouldBe(3);
		snapshot.TotalErrors.ShouldBe(8);
	}

	[Fact]
	public void Create_WithHealthWarnings_StoresCorrectly()
	{
		// Arrange
		var warnings = new List<string> { "High memory usage", "Slow response time" };

		// Act
		var snapshot = new CachePerformanceSnapshot
		{
			IsHealthy = false,
			HealthWarnings = warnings
		};

		// Assert
		snapshot.IsHealthy.ShouldBeFalse();
		snapshot.HealthWarnings.Count.ShouldBe(2);
		snapshot.HealthWarnings.ShouldContain("High memory usage");
		snapshot.HealthWarnings.ShouldContain("Slow response time");
	}

	[Fact]
	public void Create_WithAllMetrics_StoresAllProperties()
	{
		// Arrange & Act
		var snapshot = new CachePerformanceSnapshot
		{
			HitCount = 100,
			MissCount = 50,
			ItemCount = 1000,
			TotalSizeBytes = 1024000,
			EvictionCount = 10,
			AverageGetTimeMs = 1.5,
			AverageSetTimeMs = 2.0,
			P95GetTimeMs = 5.0,
			P99GetTimeMs = 10.0,
			MaxGetTimeMs = 15.0,
			MemoryPressure = 75,
			CpuUsagePercent = 30.5,
			ConcurrentOperations = 5,
			PendingOperations = 2,
			ThroughputOpsPerSecond = 500.0,
			TotalErrors = 5,
			CollectionDuration = TimeSpan.FromMinutes(1),
			IsHealthy = true
		};

		// Assert
		snapshot.HitCount.ShouldBe(100);
		snapshot.MissCount.ShouldBe(50);
		snapshot.TotalRequests.ShouldBe(150);
		snapshot.HitRatio.ShouldBe(100.0 / 150.0);
		snapshot.ItemCount.ShouldBe(1000);
		snapshot.TotalSizeBytes.ShouldBe(1024000);
		snapshot.AverageSizeBytes.ShouldBe(1024);
		snapshot.EvictionCount.ShouldBe(10);
		snapshot.AverageGetTimeMs.ShouldBe(1.5);
		snapshot.AverageSetTimeMs.ShouldBe(2.0);
		snapshot.P95GetTimeMs.ShouldBe(5.0);
		snapshot.P99GetTimeMs.ShouldBe(10.0);
		snapshot.MaxGetTimeMs.ShouldBe(15.0);
		snapshot.MemoryPressure.ShouldBe(75);
		snapshot.CpuUsagePercent.ShouldBe(30.5);
		snapshot.ConcurrentOperations.ShouldBe(5);
		snapshot.PendingOperations.ShouldBe(2);
		snapshot.ThroughputOpsPerSecond.ShouldBe(500.0);
		snapshot.TotalErrors.ShouldBe(5);
		snapshot.ErrorRate.ShouldBe(5.0 / 150.0);
		snapshot.CollectionDuration.ShouldBe(TimeSpan.FromMinutes(1));
		snapshot.IsHealthy.ShouldBeTrue();
	}
}
