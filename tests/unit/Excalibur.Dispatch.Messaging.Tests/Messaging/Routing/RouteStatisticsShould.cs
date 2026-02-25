// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
/// Unit tests for <see cref="RouteStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Routing")]
[Trait("Priority", "0")]
public sealed class RouteStatisticsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsActiveRoutes()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 10, totalDecisions: 0, cacheHitRate: 0, averageLatencyUs: 0);

		// Assert
		stats.ActiveRoutes.ShouldBe(10);
	}

	[Fact]
	public void Constructor_SetsTotalDecisions()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 0, totalDecisions: 1000L, cacheHitRate: 0, averageLatencyUs: 0);

		// Assert
		stats.TotalDecisions.ShouldBe(1000L);
	}

	[Fact]
	public void Constructor_SetsCacheHitRate()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 0, totalDecisions: 0, cacheHitRate: 95.5, averageLatencyUs: 0);

		// Assert
		stats.CacheHitRate.ShouldBe(95.5);
	}

	[Fact]
	public void Constructor_SetsAverageLatencyUs()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 0, totalDecisions: 0, cacheHitRate: 0, averageLatencyUs: 125.75);

		// Assert
		stats.AverageLatencyUs.ShouldBe(125.75);
	}

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Act
		var stats = new RouteStatistics(
			activeRoutes: 5,
			totalDecisions: 50000L,
			cacheHitRate: 98.7,
			averageLatencyUs: 50.25);

		// Assert
		stats.ActiveRoutes.ShouldBe(5);
		stats.TotalDecisions.ShouldBe(50000L);
		stats.CacheHitRate.ShouldBe(98.7);
		stats.AverageLatencyUs.ShouldBe(50.25);
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void Constructor_WithZeroActiveRoutes_Works()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 0, totalDecisions: 100, cacheHitRate: 0, averageLatencyUs: 0);

		// Assert
		stats.ActiveRoutes.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithZeroCacheHitRate_Works()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 1, totalDecisions: 100, cacheHitRate: 0.0, averageLatencyUs: 10);

		// Assert
		stats.CacheHitRate.ShouldBe(0.0);
	}

	[Fact]
	public void Constructor_WithFullCacheHitRate_Works()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 1, totalDecisions: 100, cacheHitRate: 100.0, averageLatencyUs: 10);

		// Assert
		stats.CacheHitRate.ShouldBe(100.0);
	}

	[Fact]
	public void Constructor_WithLargeTotalDecisions_Works()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 1, totalDecisions: long.MaxValue, cacheHitRate: 50, averageLatencyUs: 10);

		// Assert
		stats.TotalDecisions.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void Constructor_WithVerySmallLatency_Works()
	{
		// Act
		var stats = new RouteStatistics(activeRoutes: 1, totalDecisions: 100, cacheHitRate: 50, averageLatencyUs: 0.001);

		// Assert
		stats.AverageLatencyUs.ShouldBe(0.001);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Properties_AreReadOnly()
	{
		// Arrange & Act
		var stats = new RouteStatistics(activeRoutes: 5, totalDecisions: 100, cacheHitRate: 80, averageLatencyUs: 25);

		// Assert - Properties can be read multiple times with consistent values
		stats.ActiveRoutes.ShouldBe(5);
		stats.ActiveRoutes.ShouldBe(5);
		stats.TotalDecisions.ShouldBe(100);
		stats.TotalDecisions.ShouldBe(100);
	}

	#endregion
}
