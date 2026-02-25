// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="MessageEnvelopePoolStats"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MessageEnvelopePoolStatsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TotalRentals_IsZero()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		stats.TotalRentals.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalReturns_IsZero()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		stats.TotalReturns.ShouldBe(0);
	}

	[Fact]
	public void Default_PoolHits_IsZero()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		stats.PoolHits.ShouldBe(0);
	}

	[Fact]
	public void Default_PoolMisses_IsZero()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		stats.PoolMisses.ShouldBe(0);
	}

	[Fact]
	public void Default_HitRate_IsZero()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		stats.HitRate.ShouldBe(0.0);
	}

	[Fact]
	public void Default_ThreadLocalStats_IsEmptyArray()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats();

		// Assert
		_ = stats.ThreadLocalStats.ShouldNotBeNull();
		stats.ThreadLocalStats.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TotalRentals_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();

		// Act
		stats.TotalRentals = 100;

		// Assert
		stats.TotalRentals.ShouldBe(100);
	}

	[Fact]
	public void TotalReturns_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();

		// Act
		stats.TotalReturns = 95;

		// Assert
		stats.TotalReturns.ShouldBe(95);
	}

	[Fact]
	public void PoolHits_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();

		// Act
		stats.PoolHits = 80;

		// Assert
		stats.PoolHits.ShouldBe(80);
	}

	[Fact]
	public void PoolMisses_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();

		// Act
		stats.PoolMisses = 20;

		// Assert
		stats.PoolMisses.ShouldBe(20);
	}

	[Fact]
	public void HitRate_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();

		// Act
		stats.HitRate = 0.8;

		// Assert
		stats.HitRate.ShouldBe(0.8);
	}

	[Fact]
	public void ThreadLocalStats_CanBeSet()
	{
		// Arrange
		var stats = new MessageEnvelopePoolStats();
		var threadStats = new[]
		{
			new ThreadLocalPoolStats { CachedItems = 10, MaxSize = 100 },
			new ThreadLocalPoolStats { CachedItems = 20, MaxSize = 100 },
		};

		// Act
		stats.ThreadLocalStats = threadStats;

		// Assert
		stats.ThreadLocalStats.ShouldBe(threadStats);
		stats.ThreadLocalStats.Length.ShouldBe(2);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var stats = new MessageEnvelopePoolStats
		{
			TotalRentals = 1000,
			TotalReturns = 950,
			PoolHits = 800,
			PoolMisses = 200,
			HitRate = 0.8,
		};

		// Assert
		stats.TotalRentals.ShouldBe(1000);
		stats.TotalReturns.ShouldBe(950);
		stats.PoolHits.ShouldBe(800);
		stats.PoolMisses.ShouldBe(200);
		stats.HitRate.ShouldBe(0.8);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Stats_ForThroughputPool_HasHighHitRate()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats
		{
			TotalRentals = 10000,
			TotalReturns = 9999,
			PoolHits = 9900,
			PoolMisses = 100,
			HitRate = 0.99,
		};

		// Assert
		stats.HitRate.ShouldBeGreaterThanOrEqualTo(0.99);
		stats.TotalReturns.ShouldBe(stats.TotalRentals - 1);
	}

	[Fact]
	public void Stats_ForColdStart_HasLowHitRate()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats
		{
			TotalRentals = 100,
			TotalReturns = 100,
			PoolHits = 0,
			PoolMisses = 100,
			HitRate = 0.0,
		};

		// Assert
		stats.HitRate.ShouldBe(0.0);
		stats.PoolMisses.ShouldBe(stats.TotalRentals);
	}

	[Fact]
	public void Stats_ForMultithreaded_TracksThreadLocalStats()
	{
		// Arrange & Act
		var stats = new MessageEnvelopePoolStats
		{
			TotalRentals = 100,
			TotalReturns = 100,
			ThreadLocalStats =
			[
				new() { CachedItems = 25, MaxSize = 100 },
				new() { CachedItems = 30, MaxSize = 100 },
				new() { CachedItems = 25, MaxSize = 100 },
				new() { CachedItems = 20, MaxSize = 100 },
			],
		};

		// Assert
		stats.ThreadLocalStats.Length.ShouldBe(4);
		((long)stats.ThreadLocalStats.Sum(s => s.CachedItems)).ShouldBe(stats.TotalRentals);
	}

	#endregion
}
