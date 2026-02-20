using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests.Messaging;

/// <summary>
/// Unit tests for CacheStatistics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CacheStatisticsShould : UnitTestBase
{
	[Fact]
	public void Defaults_AreZero()
	{
		// Act
		var stats = new CacheStatistics();

		// Assert
		stats.Hits.ShouldBe(0);
		stats.Misses.ShouldBe(0);
		stats.Evictions.ShouldBe(0);
		stats.Expirations.ShouldBe(0);
		stats.ItemCount.ShouldBe(0);
		stats.TotalSizeBytes.ShouldBe(0);
		stats.HitRate.ShouldBe(0);
		stats.HitRatio.ShouldBe(0);
		stats.MissRate.ShouldBe(0);
	}

	[Fact]
	public void IncrementHits_IncrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.IncrementHits();
		stats.IncrementHits();

		// Assert
		stats.Hits.ShouldBe(2);
		stats.HitCount.ShouldBe(2);
		stats.CacheHits.ShouldBe(2);
	}

	[Fact]
	public void IncrementMisses_IncrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.IncrementMisses();

		// Assert
		stats.Misses.ShouldBe(1);
		stats.MissCount.ShouldBe(1);
		stats.CacheMisses.ShouldBe(1);
	}

	[Fact]
	public void IncrementEvictions_IncrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.IncrementEvictions();

		// Assert
		stats.Evictions.ShouldBe(1);
	}

	[Fact]
	public void IncrementExpirations_IncrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.IncrementExpirations();

		// Assert
		stats.Expirations.ShouldBe(1);
	}

	[Fact]
	public void IncrementEntryCount_IncrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.IncrementEntryCount();
		stats.IncrementEntryCount();

		// Assert
		stats.ItemCount.ShouldBe(2);
		stats.EntryCount.ShouldBe(2);
		stats.CurrentSize.ShouldBe(2);
		stats.CacheSize.ShouldBe(2);
		stats.CurrentCacheSize.ShouldBe(2);
	}

	[Fact]
	public void DecrementEntryCount_DecrementsCounter()
	{
		// Arrange
		var stats = new CacheStatistics { ItemCount = 5 };

		// Act
		stats.DecrementEntryCount();

		// Assert
		stats.ItemCount.ShouldBe(4);
	}

	[Fact]
	public void AddSizeBytes_AddsToTotal()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.AddSizeBytes(1024);

		// Assert
		stats.TotalSizeBytes.ShouldBe(1024);
		stats.SizeInBytes.ShouldBe(1024);
		stats.TotalSize.ShouldBe(1024);
		stats.MemoryUsage.ShouldBe(1024);
	}

	[Fact]
	public void SubtractSizeBytes_SubtractsFromTotal()
	{
		// Arrange
		var stats = new CacheStatistics { TotalSizeBytes = 2048 };

		// Act
		stats.SubtractSizeBytes(1024);

		// Assert
		stats.TotalSizeBytes.ShouldBe(1024);
	}

	[Fact]
	public void HitRate_CalculatesCorrectly()
	{
		// Arrange
		var stats = new CacheStatistics { Hits = 75, Misses = 25 };

		// Act & Assert
		stats.HitRate.ShouldBe(75.0);
		stats.HitRatio.ShouldBe(0.75);
		stats.MissRate.ShouldBe(25.0);
		stats.CacheHitRate.ShouldBe(75.0);
	}

	[Fact]
	public void TotalRequests_WhenNotSet_SumsHitsAndMisses()
	{
		// Arrange
		var stats = new CacheStatistics { Hits = 50, Misses = 50 };

		// Act & Assert
		stats.TotalRequests.ShouldBe(100);
		stats.TotalAccesses.ShouldBe(100);
	}

	[Fact]
	public void TotalRequests_WhenSet_UsesSetValue()
	{
		// Arrange
		var stats = new CacheStatistics { Hits = 50, Misses = 50, TotalRequests = 200 };

		// Act & Assert
		stats.TotalRequests.ShouldBe(200);
	}

	[Fact]
	public void SagaId_ReadsAndWritesCacheId()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act
		stats.SagaId = "saga-123";

		// Assert
		stats.CacheId.ShouldBe("saga-123");
		stats.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void SagaId_WhenCacheIdNull_ReturnsEmpty()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act & Assert
		stats.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void AliasProperties_SyncWithPrimary()
	{
		// Arrange
		var stats = new CacheStatistics();

		// Act - set via alias
		stats.EntryCount = 10;
		stats.SizeInBytes = 2048;
		stats.HitCount = 100;
		stats.MissCount = 50;
		stats.TotalAccesses = 200;
		stats.CurrentSize = 15;
		stats.CacheSize = 20;
		stats.CurrentCacheSize = 25;
		stats.TotalSize = 4096;
		stats.MemoryUsage = 8192;
		stats.CacheHits = 300;
		stats.CacheMisses = 100;

		// Assert - read via primary
		stats.ItemCount.ShouldBe(25);
		stats.TotalSizeBytes.ShouldBe(8192);
		stats.Hits.ShouldBe(300);
		stats.Misses.ShouldBe(100);
		stats.TotalRequests.ShouldBe(200);
	}

	[Fact]
	public void HitRate_Setter_DoesNothing()
	{
		// Arrange
		var stats = new CacheStatistics { Hits = 50, Misses = 50 };

		// Act - HitRate setter is no-op for backward compat
		stats.HitRate = 99.0;
		stats.CacheHitRate = 99.0;

		// Assert - calculated value unchanged
		stats.HitRate.ShouldBe(50.0);
	}

	[Fact]
	public void Reset_ClearsAllCounters()
	{
		// Arrange
		var stats = new CacheStatistics
		{
			Hits = 100,
			Misses = 50,
			Evictions = 10,
			Expirations = 5,
			ItemCount = 25,
			TotalSizeBytes = 1024,
			TotalRequests = 150,
			MaxSize = 1000,
			AverageAccessTime = TimeSpan.FromMilliseconds(5),
			AverageAccessInterval = TimeSpan.FromMinutes(1),
			AverageGetTimeMs = 2.5,
			AverageSetTimeMs = 1.0,
			RecommendedTtl = TimeSpan.FromMinutes(10),
		};

		// Act
		stats.Reset();

		// Assert
		stats.Hits.ShouldBe(0);
		stats.Misses.ShouldBe(0);
		stats.Evictions.ShouldBe(0);
		stats.Expirations.ShouldBe(0);
		stats.ItemCount.ShouldBe(0);
		stats.TotalSizeBytes.ShouldBe(0);
		stats.AverageAccessTime.ShouldBe(TimeSpan.Zero);
		stats.AverageAccessInterval.ShouldBe(TimeSpan.Zero);
		stats.AverageGetTimeMs.ShouldBe(0);
		stats.AverageSetTimeMs.ShouldBe(0);
		stats.RecommendedTtl.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateSnapshot_CopiesAllValues()
	{
		// Arrange
		var stats = new CacheStatistics
		{
			CacheId = "cache-1",
			Hits = 100,
			Misses = 50,
			TotalRequests = 200,
			Evictions = 10,
			Expirations = 5,
			ItemCount = 25,
			TotalSizeBytes = 1024,
			MaxSize = 1000,
			AverageAccessTime = TimeSpan.FromMilliseconds(5),
			AverageGetTimeMs = 2.5,
			AverageSetTimeMs = 1.0,
			RecommendedTtl = TimeSpan.FromMinutes(10),
		};

		// Act
		var snapshot = stats.CreateSnapshot();

		// Assert
		snapshot.CacheId.ShouldBe("cache-1");
		snapshot.Hits.ShouldBe(100);
		snapshot.Misses.ShouldBe(50);
		snapshot.TotalRequests.ShouldBe(200);
		snapshot.Evictions.ShouldBe(10);
		snapshot.Expirations.ShouldBe(5);
		snapshot.ItemCount.ShouldBe(25);
		snapshot.TotalSizeBytes.ShouldBe(1024);
		snapshot.MaxSize.ShouldBe(1000);
		snapshot.AverageGetTimeMs.ShouldBe(2.5);
		snapshot.AverageSetTimeMs.ShouldBe(1.0);
	}

	[Fact]
	public void CreateSnapshot_IsIndependentCopy()
	{
		// Arrange
		var stats = new CacheStatistics { Hits = 100 };

		// Act
		var snapshot = stats.CreateSnapshot();
		stats.Hits = 200;

		// Assert - snapshot is independent
		snapshot.Hits.ShouldBe(100);
	}

	[Fact]
	public void Properties_CanSetAndGet()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var stats = new CacheStatistics
		{
			MaxSize = 500,
			LastAccessTime = now,
			LastResetTime = now,
			LastReset = new DateTimeOffset(now, TimeSpan.Zero),
		};

		// Assert
		stats.MaxSize.ShouldBe(500);
		stats.LastAccessTime.ShouldBe(now);
		stats.LastResetTime.ShouldBe(now);
	}
}
