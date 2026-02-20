// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Unit tests for <see cref="HandlerRegistryMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
[Trait("Priority", "0")]
public sealed class HandlerRegistryMetricsShould
{
	#region Object Initializer Tests

	[Fact]
	public void Construct_WithRequiredProperties_SetsAllProperties()
	{
		// Arrange
		var totalLookups = 100;
		var totalLookupTime = TimeSpan.FromMilliseconds(500);
		var averageLookupTime = TimeSpan.FromMilliseconds(5);
		var averageHandlersPerLookup = 2.5;
		var cacheHits = 80;
		var cacheMisses = 20;
		var cacheHitRate = 0.8;

		// Act
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = totalLookups,
			TotalLookupTime = totalLookupTime,
			AverageLookupTime = averageLookupTime,
			AverageHandlersPerLookup = averageHandlersPerLookup,
			CacheHits = cacheHits,
			CacheMisses = cacheMisses,
			CacheHitRate = cacheHitRate,
		};

		// Assert
		metrics.TotalLookups.ShouldBe(totalLookups);
		metrics.TotalLookupTime.ShouldBe(totalLookupTime);
		metrics.AverageLookupTime.ShouldBe(averageLookupTime);
		metrics.AverageHandlersPerLookup.ShouldBe(averageHandlersPerLookup);
		metrics.CacheHits.ShouldBe(cacheHits);
		metrics.CacheMisses.ShouldBe(cacheMisses);
		metrics.CacheHitRate.ShouldBe(cacheHitRate);
	}

	[Fact]
	public void Construct_WithZeroValues_SetsAllPropertiesToZero()
	{
		// Act
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = 0,
			TotalLookupTime = TimeSpan.Zero,
			AverageLookupTime = TimeSpan.Zero,
			AverageHandlersPerLookup = 0.0,
			CacheHits = 0,
			CacheMisses = 0,
			CacheHitRate = 0.0,
		};

		// Assert
		metrics.TotalLookups.ShouldBe(0);
		metrics.TotalLookupTime.ShouldBe(TimeSpan.Zero);
		metrics.AverageLookupTime.ShouldBe(TimeSpan.Zero);
		metrics.AverageHandlersPerLookup.ShouldBe(0.0);
		metrics.CacheHits.ShouldBe(0);
		metrics.CacheMisses.ShouldBe(0);
		metrics.CacheHitRate.ShouldBe(0.0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = CreateStandardMetrics();

		// Act & Assert
		metrics1.ShouldBe(metrics2);
		(metrics1 == metrics2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentTotalLookups_ReturnsFalse()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = metrics1 with { TotalLookups = 200 };

		// Act & Assert
		metrics1.ShouldNotBe(metrics2);
	}

	[Fact]
	public void Equals_WithDifferentCacheHitRate_ReturnsFalse()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = metrics1 with { CacheHitRate = 0.5 };

		// Act & Assert
		metrics1.ShouldNotBe(metrics2);
	}

	[Fact]
	public void GetHashCode_WithSameValues_ReturnsSameHashCode()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = CreateStandardMetrics();

		// Act & Assert
		metrics1.GetHashCode().ShouldBe(metrics2.GetHashCode());
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void WithExpression_CreatesCopyWithModifiedProperty()
	{
		// Arrange
		var original = CreateStandardMetrics();

		// Act
		var modified = original with { TotalLookups = 500 };

		// Assert
		modified.TotalLookups.ShouldBe(500);
		modified.CacheHits.ShouldBe(original.CacheHits);
		modified.CacheMisses.ShouldBe(original.CacheMisses);
	}

	[Fact]
	public void WithExpression_PreservesUnmodifiedProperties()
	{
		// Arrange
		var original = CreateStandardMetrics();

		// Act
		var modified = original with { CacheHitRate = 0.95 };

		// Assert
		modified.CacheHitRate.ShouldBe(0.95);
		modified.TotalLookups.ShouldBe(original.TotalLookups);
		modified.TotalLookupTime.ShouldBe(original.TotalLookupTime);
		modified.AverageLookupTime.ShouldBe(original.AverageLookupTime);
		modified.AverageHandlersPerLookup.ShouldBe(original.AverageHandlersPerLookup);
		modified.CacheHits.ShouldBe(original.CacheHits);
		modified.CacheMisses.ShouldBe(original.CacheMisses);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Metrics_ForThroughputScenario_HasHighCacheHitRate()
	{
		// Act
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = 10000,
			TotalLookupTime = TimeSpan.FromMilliseconds(100),
			AverageLookupTime = TimeSpan.FromMicroseconds(10),
			AverageHandlersPerLookup = 1.0,
			CacheHits = 9900,
			CacheMisses = 100,
			CacheHitRate = 0.99,
		};

		// Assert
		metrics.CacheHitRate.ShouldBeGreaterThanOrEqualTo(0.99);
		metrics.AverageLookupTime.TotalMicroseconds.ShouldBeLessThan(100);
	}

	[Fact]
	public void Metrics_ForMultiHandlerScenario_ReflectsAverageHandlers()
	{
		// Act
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = 50,
			TotalLookupTime = TimeSpan.FromMilliseconds(250),
			AverageLookupTime = TimeSpan.FromMilliseconds(5),
			AverageHandlersPerLookup = 3.5,
			CacheHits = 45,
			CacheMisses = 5,
			CacheHitRate = 0.9,
		};

		// Assert
		metrics.AverageHandlersPerLookup.ShouldBeGreaterThan(1.0);
	}

	[Fact]
	public void Metrics_ForColdStartScenario_HasLowCacheHitRate()
	{
		// Act
		var metrics = new HandlerRegistryMetrics
		{
			TotalLookups = 10,
			TotalLookupTime = TimeSpan.FromMilliseconds(500),
			AverageLookupTime = TimeSpan.FromMilliseconds(50),
			AverageHandlersPerLookup = 1.0,
			CacheHits = 0,
			CacheMisses = 10,
			CacheHitRate = 0.0,
		};

		// Assert
		metrics.CacheHitRate.ShouldBe(0.0);
		metrics.CacheMisses.ShouldBe(metrics.TotalLookups);
	}

	#endregion

	#region Helper Methods

	private static HandlerRegistryMetrics CreateStandardMetrics() =>
		new()
		{
			TotalLookups = 100,
			TotalLookupTime = TimeSpan.FromMilliseconds(500),
			AverageLookupTime = TimeSpan.FromMilliseconds(5),
			AverageHandlersPerLookup = 2.0,
			CacheHits = 80,
			CacheMisses = 20,
			CacheHitRate = 0.8,
		};

	#endregion
}
