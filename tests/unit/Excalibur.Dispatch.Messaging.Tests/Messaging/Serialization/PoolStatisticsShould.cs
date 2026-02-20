// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Unit tests for <see cref="PoolStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class PoolStatisticsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_CurrentPoolSize_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.CurrentPoolSize.ShouldBe(0);
	}

	[Fact]
	public void Default_MaxPoolSize_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.MaxPoolSize.ShouldBe(0);
	}

	[Fact]
	public void Default_PeakPoolSize_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.PeakPoolSize.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalRented_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.TotalRented.ShouldBe(0L);
	}

	[Fact]
	public void Default_TotalReturned_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.TotalReturned.ShouldBe(0L);
	}

	[Fact]
	public void Default_ThreadLocalHits_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.ThreadLocalHits.ShouldBe(0L);
	}

	[Fact]
	public void Default_ThreadLocalMisses_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.ThreadLocalMisses.ShouldBe(0L);
	}

	[Fact]
	public void Default_ThreadLocalHitRate_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.ThreadLocalHitRate.ShouldBe(0.0);
	}

	[Fact]
	public void Default_OptionMismatches_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.OptionMismatches.ShouldBe(0L);
	}

	[Fact]
	public void Default_PoolExpansions_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.PoolExpansions.ShouldBe(0L);
	}

	[Fact]
	public void Default_PoolContractions_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.PoolContractions.ShouldBe(0L);
	}

	[Fact]
	public void Default_ActiveWriters_IsZero()
	{
		// Arrange & Act
		var stats = new PoolStatistics();

		// Assert
		stats.ActiveWriters.ShouldBe(0L);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var stats = new PoolStatistics
		{
			CurrentPoolSize = 10,
			MaxPoolSize = 100,
			PeakPoolSize = 50,
			TotalRented = 1000L,
			TotalReturned = 990L,
			ThreadLocalHits = 800L,
			ThreadLocalMisses = 200L,
			ThreadLocalHitRate = 0.8,
			OptionMismatches = 5L,
			PoolExpansions = 3L,
			PoolContractions = 1L,
			ActiveWriters = 10L,
		};

		// Assert
		stats.CurrentPoolSize.ShouldBe(10);
		stats.MaxPoolSize.ShouldBe(100);
		stats.PeakPoolSize.ShouldBe(50);
		stats.TotalRented.ShouldBe(1000L);
		stats.TotalReturned.ShouldBe(990L);
		stats.ThreadLocalHits.ShouldBe(800L);
		stats.ThreadLocalMisses.ShouldBe(200L);
		stats.ThreadLocalHitRate.ShouldBe(0.8);
		stats.OptionMismatches.ShouldBe(5L);
		stats.PoolExpansions.ShouldBe(3L);
		stats.PoolContractions.ShouldBe(1L);
		stats.ActiveWriters.ShouldBe(10L);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Stats_ForHighlyUtilizedPool_HasHighHitRate()
	{
		// Act
		var stats = new PoolStatistics
		{
			CurrentPoolSize = 50,
			MaxPoolSize = 100,
			TotalRented = 10000L,
			TotalReturned = 10000L,
			ThreadLocalHits = 9500L,
			ThreadLocalMisses = 500L,
			ThreadLocalHitRate = 0.95,
			ActiveWriters = 0L,
		};

		// Assert
		stats.ThreadLocalHitRate.ShouldBeGreaterThan(0.9);
		stats.ActiveWriters.ShouldBe(0L); // All returned
	}

	[Fact]
	public void Stats_WithActiveWriters_ShowsLeaks()
	{
		// Act
		var stats = new PoolStatistics
		{
			TotalRented = 1000L,
			TotalReturned = 990L,
			ActiveWriters = 10L,
		};

		// Assert - Active writers should equal rented minus returned
		stats.ActiveWriters.ShouldBe(stats.TotalRented - stats.TotalReturned);
	}

	#endregion
}
