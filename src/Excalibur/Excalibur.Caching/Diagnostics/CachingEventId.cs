// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.Diagnostics;

/// <summary>
/// Event IDs for caching infrastructure (150000-151999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>150000-150499: Cache Core</item>
/// <item>150500-150999: Distributed Cache</item>
/// <item>151000-151499: Cache Invalidation</item>
/// <item>151500-151999: Cache Statistics</item>
/// </list>
/// </remarks>
public static class CachingEventId
{
	// ========================================
	// 150000-150099: Cache Core
	// ========================================

	/// <summary>Cache service created.</summary>
	public const int CacheServiceCreated = 150000;

	/// <summary>Cache entry set.</summary>
	public const int CacheEntrySet = 150001;

	/// <summary>Cache entry retrieved.</summary>
	public const int CacheEntryRetrieved = 150002;

	/// <summary>Cache entry removed.</summary>
	public const int CacheEntryRemoved = 150003;

	/// <summary>Cache hit.</summary>
	public const int CacheHit = 150004;

	/// <summary>Cache miss.</summary>
	public const int CacheMiss = 150005;

	// ========================================
	// 150100-150199: Cache Operations
	// ========================================

	/// <summary>Cache entry expired.</summary>
	public const int CacheEntryExpired = 150100;

	/// <summary>Cache entry evicted.</summary>
	public const int CacheEntryEvicted = 150101;

	/// <summary>Cache cleared.</summary>
	public const int CacheCleared = 150102;

	/// <summary>Cache key generated.</summary>
	public const int CacheKeyGenerated = 150103;

	/// <summary>Cache refresh triggered.</summary>
	public const int CacheRefreshTriggered = 150104;

	// ========================================
	// 150200-150299: In-Memory Cache
	// ========================================

	/// <summary>In-memory cache created.</summary>
	public const int InMemoryCacheCreated = 150200;

	/// <summary>Memory pressure detected.</summary>
	public const int MemoryPressureDetected = 150201;

	/// <summary>Memory cache compacted.</summary>
	public const int MemoryCacheCompacted = 150202;

	/// <summary>Memory limit reached.</summary>
	public const int MemoryLimitReached = 150203;

	// ========================================
	// 150500-150599: Distributed Cache Core
	// ========================================

	/// <summary>Distributed cache created.</summary>
	public const int DistributedCacheCreated = 150500;

	/// <summary>Distributed cache connected.</summary>
	public const int DistributedCacheConnected = 150501;

	/// <summary>Distributed cache disconnected.</summary>
	public const int DistributedCacheDisconnected = 150502;

	/// <summary>Distributed cache operation completed.</summary>
	public const int DistributedCacheOperationCompleted = 150503;

	/// <summary>Distributed cache operation failed.</summary>
	public const int DistributedCacheOperationFailed = 150504;

	// ========================================
	// 150600-150699: Redis Cache
	// ========================================

	/// <summary>Redis cache created.</summary>
	public const int RedisCacheCreated = 150600;

	/// <summary>Redis connection established.</summary>
	public const int RedisConnectionEstablished = 150601;

	/// <summary>Redis command executed.</summary>
	public const int RedisCommandExecuted = 150602;

	/// <summary>Redis pipeline executed.</summary>
	public const int RedisPipelineExecuted = 150603;

	/// <summary>Redis cluster configured.</summary>
	public const int RedisClusterConfigured = 150604;

	// ========================================
	// 151000-151099: Cache Invalidation Core
	// ========================================

	/// <summary>Cache invalidation triggered.</summary>
	public const int CacheInvalidationTriggered = 151000;

	/// <summary>Cache invalidation completed.</summary>
	public const int CacheInvalidationCompleted = 151001;

	/// <summary>Cache keys invalidated.</summary>
	public const int CacheKeysInvalidated = 151002;

	/// <summary>Cache tag invalidated.</summary>
	public const int CacheTagInvalidated = 151003;

	/// <summary>Cache pattern invalidated.</summary>
	public const int CachePatternInvalidated = 151004;

	// ========================================
	// 151100-151199: Cache Synchronization
	// ========================================

	/// <summary>Cache sync message sent.</summary>
	public const int CacheSyncMessageSent = 151100;

	/// <summary>Cache sync message received.</summary>
	public const int CacheSyncMessageReceived = 151101;

	/// <summary>Cache sync completed.</summary>
	public const int CacheSyncCompleted = 151102;

	/// <summary>Cache sync conflict resolved.</summary>
	public const int CacheSyncConflictResolved = 151103;

	// ========================================
	// 151500-151599: Cache Statistics
	// ========================================

	/// <summary>Cache statistics collected.</summary>
	public const int CacheStatisticsCollected = 151500;

	/// <summary>Cache hit ratio calculated.</summary>
	public const int CacheHitRatioCalculated = 151501;

	/// <summary>Cache size reported.</summary>
	public const int CacheSizeReported = 151502;

	/// <summary>Cache performance metrics recorded.</summary>
	public const int CachePerformanceMetricsRecorded = 151503;

	/// <summary>Cache health reported.</summary>
	public const int CacheHealthReported = 151504;

	// ========================================
	// 151600-151699: Projection Cache
	// ========================================

	/// <summary>Invalidating projection cache tags.</summary>
	public const int InvalidatingProjectionCacheTags = 151600;

	/// <summary>Resolver invocation failed during tag extraction.</summary>
	public const int ResolverInvocationFailed = 151601;

	/// <summary>No invalidation strategy matched for a message type.</summary>
	public const int NoInvalidationStrategyMatched = 151602;

	/// <summary>No tag resolver registered for a message type.</summary>
	public const int NoTagResolverRegistered = 151603;

	/// <summary>Convention-based tag extraction found no matching property.</summary>
	public const int ConventionPropertyNotFound = 151604;

	/// <summary>Convention-based tag extraction found a property but it returned a null/empty value.</summary>
	public const int ConventionPropertyValueEmpty = 151605;

	// ========================================
	// 151700-151799: Adaptive TTL - ML Strategy
	// ========================================

	/// <summary>Adaptive TTL calculated.</summary>
	public const int AdaptiveTtlCalculated = 151700;

	/// <summary>Adaptive TTL strategy updated.</summary>
	public const int AdaptiveTtlStrategyUpdated = 151701;

	// ========================================
	// 151800-151899: Adaptive TTL Cache
	// ========================================

	/// <summary>Adaptive cache operation.</summary>
	public const int AdaptiveCacheOperation = 151800;

	/// <summary>Adaptive cache error.</summary>
	public const int AdaptiveCacheError = 151801;

	/// <summary>Setting cache key with adaptive TTL.</summary>
	public const int SetCacheKey = 151802;

	/// <summary>Metadata cleanup.</summary>
	public const int MetadataCleanup = 151803;

	/// <summary>Cleanup error.</summary>
	public const int CleanupError = 151804;

	// ========================================
	// 151900-151999: Adaptive TTL - Rule Strategy
	// ========================================

	/// <summary>Rule-based TTL calculated.</summary>
	public const int RuleBasedTtlCalculated = 151900;

	/// <summary>Rule-based metrics updated.</summary>
	public const int RuleBasedMetricsUpdated = 151901;
}
