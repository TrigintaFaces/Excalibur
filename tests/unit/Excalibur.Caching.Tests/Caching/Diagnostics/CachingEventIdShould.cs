// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Diagnostics;

namespace Excalibur.Tests.Caching.Diagnostics;

/// <summary>
/// Unit tests for <see cref="CachingEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class CachingEventIdShould : UnitTestBase
{
	#region Cache Core Event ID Tests (150000-150099)

	[Fact]
	public void HaveCacheServiceCreatedInCacheCoreRange()
	{
		CachingEventId.CacheServiceCreated.ShouldBe(150000);
	}

	[Fact]
	public void HaveAllCacheCoreEventIdsInExpectedRange()
	{
		CachingEventId.CacheServiceCreated.ShouldBeInRange(150000, 150099);
		CachingEventId.CacheEntrySet.ShouldBeInRange(150000, 150099);
		CachingEventId.CacheEntryRetrieved.ShouldBeInRange(150000, 150099);
		CachingEventId.CacheEntryRemoved.ShouldBeInRange(150000, 150099);
		CachingEventId.CacheHit.ShouldBeInRange(150000, 150099);
		CachingEventId.CacheMiss.ShouldBeInRange(150000, 150099);
	}

	#endregion

	#region Cache Operations Event ID Tests (150100-150199)

	[Fact]
	public void HaveCacheEntryExpiredInCacheOperationsRange()
	{
		CachingEventId.CacheEntryExpired.ShouldBe(150100);
	}

	[Fact]
	public void HaveAllCacheOperationsEventIdsInExpectedRange()
	{
		CachingEventId.CacheEntryExpired.ShouldBeInRange(150100, 150199);
		CachingEventId.CacheEntryEvicted.ShouldBeInRange(150100, 150199);
		CachingEventId.CacheCleared.ShouldBeInRange(150100, 150199);
		CachingEventId.CacheKeyGenerated.ShouldBeInRange(150100, 150199);
		CachingEventId.CacheRefreshTriggered.ShouldBeInRange(150100, 150199);
	}

	#endregion

	#region In-Memory Cache Event ID Tests (150200-150299)

	[Fact]
	public void HaveInMemoryCacheCreatedInInMemoryCacheRange()
	{
		CachingEventId.InMemoryCacheCreated.ShouldBe(150200);
	}

	[Fact]
	public void HaveAllInMemoryCacheEventIdsInExpectedRange()
	{
		CachingEventId.InMemoryCacheCreated.ShouldBeInRange(150200, 150299);
		CachingEventId.MemoryPressureDetected.ShouldBeInRange(150200, 150299);
		CachingEventId.MemoryCacheCompacted.ShouldBeInRange(150200, 150299);
		CachingEventId.MemoryLimitReached.ShouldBeInRange(150200, 150299);
	}

	#endregion

	#region Distributed Cache Event ID Tests (150500-150599)

	[Fact]
	public void HaveDistributedCacheCreatedInDistributedCacheRange()
	{
		CachingEventId.DistributedCacheCreated.ShouldBe(150500);
	}

	[Fact]
	public void HaveAllDistributedCacheEventIdsInExpectedRange()
	{
		CachingEventId.DistributedCacheCreated.ShouldBeInRange(150500, 150599);
		CachingEventId.DistributedCacheConnected.ShouldBeInRange(150500, 150599);
		CachingEventId.DistributedCacheDisconnected.ShouldBeInRange(150500, 150599);
		CachingEventId.DistributedCacheOperationCompleted.ShouldBeInRange(150500, 150599);
		CachingEventId.DistributedCacheOperationFailed.ShouldBeInRange(150500, 150599);
	}

	#endregion

	#region Redis Cache Event ID Tests (150600-150699)

	[Fact]
	public void HaveRedisCacheCreatedInRedisCacheRange()
	{
		CachingEventId.RedisCacheCreated.ShouldBe(150600);
	}

	[Fact]
	public void HaveAllRedisCacheEventIdsInExpectedRange()
	{
		CachingEventId.RedisCacheCreated.ShouldBeInRange(150600, 150699);
		CachingEventId.RedisConnectionEstablished.ShouldBeInRange(150600, 150699);
		CachingEventId.RedisCommandExecuted.ShouldBeInRange(150600, 150699);
		CachingEventId.RedisPipelineExecuted.ShouldBeInRange(150600, 150699);
		CachingEventId.RedisClusterConfigured.ShouldBeInRange(150600, 150699);
	}

	#endregion

	#region Cache Invalidation Event ID Tests (151000-151099)

	[Fact]
	public void HaveCacheInvalidationTriggeredInCacheInvalidationRange()
	{
		CachingEventId.CacheInvalidationTriggered.ShouldBe(151000);
	}

	[Fact]
	public void HaveAllCacheInvalidationEventIdsInExpectedRange()
	{
		CachingEventId.CacheInvalidationTriggered.ShouldBeInRange(151000, 151099);
		CachingEventId.CacheInvalidationCompleted.ShouldBeInRange(151000, 151099);
		CachingEventId.CacheKeysInvalidated.ShouldBeInRange(151000, 151099);
		CachingEventId.CacheTagInvalidated.ShouldBeInRange(151000, 151099);
		CachingEventId.CachePatternInvalidated.ShouldBeInRange(151000, 151099);
	}

	#endregion

	#region Cache Synchronization Event ID Tests (151100-151199)

	[Fact]
	public void HaveCacheSyncMessageSentInCacheSyncRange()
	{
		CachingEventId.CacheSyncMessageSent.ShouldBe(151100);
	}

	[Fact]
	public void HaveAllCacheSyncEventIdsInExpectedRange()
	{
		CachingEventId.CacheSyncMessageSent.ShouldBeInRange(151100, 151199);
		CachingEventId.CacheSyncMessageReceived.ShouldBeInRange(151100, 151199);
		CachingEventId.CacheSyncCompleted.ShouldBeInRange(151100, 151199);
		CachingEventId.CacheSyncConflictResolved.ShouldBeInRange(151100, 151199);
	}

	#endregion

	#region Cache Statistics Event ID Tests (151500-151599)

	[Fact]
	public void HaveCacheStatisticsCollectedInCacheStatisticsRange()
	{
		CachingEventId.CacheStatisticsCollected.ShouldBe(151500);
	}

	[Fact]
	public void HaveAllCacheStatisticsEventIdsInExpectedRange()
	{
		CachingEventId.CacheStatisticsCollected.ShouldBeInRange(151500, 151599);
		CachingEventId.CacheHitRatioCalculated.ShouldBeInRange(151500, 151599);
		CachingEventId.CacheSizeReported.ShouldBeInRange(151500, 151599);
		CachingEventId.CachePerformanceMetricsRecorded.ShouldBeInRange(151500, 151599);
		CachingEventId.CacheHealthReported.ShouldBeInRange(151500, 151599);
	}

	#endregion

	#region Projection Cache Event ID Tests (151600-151699)

	[Fact]
	public void HaveInvalidatingProjectionCacheTagsInProjectionCacheRange()
	{
		CachingEventId.InvalidatingProjectionCacheTags.ShouldBe(151600);
	}

	#endregion

	#region Adaptive TTL Event ID Tests (151700-151899)

	[Fact]
	public void HaveAdaptiveTtlCalculatedInAdaptiveTtlRange()
	{
		CachingEventId.AdaptiveTtlCalculated.ShouldBe(151700);
	}

	[Fact]
	public void HaveAllAdaptiveTtlEventIdsInExpectedRange()
	{
		CachingEventId.AdaptiveTtlCalculated.ShouldBeInRange(151700, 151799);
		CachingEventId.AdaptiveTtlStrategyUpdated.ShouldBeInRange(151700, 151799);
		CachingEventId.AdaptiveCacheOperation.ShouldBeInRange(151800, 151899);
		CachingEventId.AdaptiveCacheError.ShouldBeInRange(151800, 151899);
		CachingEventId.SetCacheKey.ShouldBeInRange(151800, 151899);
		CachingEventId.MetadataCleanup.ShouldBeInRange(151800, 151899);
		CachingEventId.CleanupError.ShouldBeInRange(151800, 151899);
	}

	#endregion

	#region Rule-Based TTL Event ID Tests (151900-151999)

	[Fact]
	public void HaveRuleBasedTtlCalculatedInRuleBasedTtlRange()
	{
		CachingEventId.RuleBasedTtlCalculated.ShouldBe(151900);
	}

	[Fact]
	public void HaveAllRuleBasedTtlEventIdsInExpectedRange()
	{
		CachingEventId.RuleBasedTtlCalculated.ShouldBeInRange(151900, 151999);
		CachingEventId.RuleBasedMetricsUpdated.ShouldBeInRange(151900, 151999);
	}

	#endregion

	#region Caching Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInCachingReservedRange()
	{
		// Caching reserved range is 150000-151999
		var allEventIds = GetAllCachingEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(150000, 151999,
				$"Event ID {eventId} is outside Caching reserved range (150000-151999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllCachingEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllCachingEventIds();
		allEventIds.Length.ShouldBeGreaterThan(40);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllCachingEventIds()
	{
		return
		[
			// Cache Core (150000-150099)
			CachingEventId.CacheServiceCreated,
			CachingEventId.CacheEntrySet,
			CachingEventId.CacheEntryRetrieved,
			CachingEventId.CacheEntryRemoved,
			CachingEventId.CacheHit,
			CachingEventId.CacheMiss,

			// Cache Operations (150100-150199)
			CachingEventId.CacheEntryExpired,
			CachingEventId.CacheEntryEvicted,
			CachingEventId.CacheCleared,
			CachingEventId.CacheKeyGenerated,
			CachingEventId.CacheRefreshTriggered,

			// In-Memory Cache (150200-150299)
			CachingEventId.InMemoryCacheCreated,
			CachingEventId.MemoryPressureDetected,
			CachingEventId.MemoryCacheCompacted,
			CachingEventId.MemoryLimitReached,

			// Distributed Cache (150500-150599)
			CachingEventId.DistributedCacheCreated,
			CachingEventId.DistributedCacheConnected,
			CachingEventId.DistributedCacheDisconnected,
			CachingEventId.DistributedCacheOperationCompleted,
			CachingEventId.DistributedCacheOperationFailed,

			// Redis Cache (150600-150699)
			CachingEventId.RedisCacheCreated,
			CachingEventId.RedisConnectionEstablished,
			CachingEventId.RedisCommandExecuted,
			CachingEventId.RedisPipelineExecuted,
			CachingEventId.RedisClusterConfigured,

			// Cache Invalidation (151000-151099)
			CachingEventId.CacheInvalidationTriggered,
			CachingEventId.CacheInvalidationCompleted,
			CachingEventId.CacheKeysInvalidated,
			CachingEventId.CacheTagInvalidated,
			CachingEventId.CachePatternInvalidated,

			// Cache Synchronization (151100-151199)
			CachingEventId.CacheSyncMessageSent,
			CachingEventId.CacheSyncMessageReceived,
			CachingEventId.CacheSyncCompleted,
			CachingEventId.CacheSyncConflictResolved,

			// Cache Statistics (151500-151599)
			CachingEventId.CacheStatisticsCollected,
			CachingEventId.CacheHitRatioCalculated,
			CachingEventId.CacheSizeReported,
			CachingEventId.CachePerformanceMetricsRecorded,
			CachingEventId.CacheHealthReported,

			// Projection Cache (151600-151699)
			CachingEventId.InvalidatingProjectionCacheTags,

			// Adaptive TTL - ML Strategy (151700-151799)
			CachingEventId.AdaptiveTtlCalculated,
			CachingEventId.AdaptiveTtlStrategyUpdated,

			// Adaptive TTL Cache (151800-151899)
			CachingEventId.AdaptiveCacheOperation,
			CachingEventId.AdaptiveCacheError,
			CachingEventId.SetCacheKey,
			CachingEventId.MetadataCleanup,
			CachingEventId.CleanupError,

			// Rule-Based TTL (151900-151999)
			CachingEventId.RuleBasedTtlCalculated,
			CachingEventId.RuleBasedMetricsUpdated
		];
	}

	#endregion
}
