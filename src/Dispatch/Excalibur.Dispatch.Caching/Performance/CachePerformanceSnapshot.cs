// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Represents a snapshot of cache performance metrics at a specific point in time.
/// </summary>
public sealed class CachePerformanceSnapshot
{
	/// <summary>
	/// Gets when this snapshot was taken.
	/// </summary>
	/// <value>The timestamp when this snapshot was taken.</value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the total number of cache hits.
	/// </summary>
	/// <value>The total number of cache hits.</value>
	public long HitCount { get; init; }

	/// <summary>
	/// Gets the total number of cache misses.
	/// </summary>
	/// <value>The total number of cache misses.</value>
	public long MissCount { get; init; }

	/// <summary>
	/// Gets the cache hit ratio.
	/// </summary>
	/// <value>The cache hit ratio as a value between 0 and 1.</value>
	public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;

	/// <summary>
	/// Gets the total number of cache requests.
	/// </summary>
	/// <value>The total number of cache requests (hits plus misses).</value>
	public long TotalRequests => HitCount + MissCount;

	/// <summary>
	/// Gets the number of items currently in the cache.
	/// </summary>
	/// <value>The number of items currently in the cache.</value>
	public long ItemCount { get; init; }

	/// <summary>
	/// Gets the total size of cached data in bytes.
	/// </summary>
	/// <value>The total size of cached data in bytes.</value>
	public long TotalSizeBytes { get; init; }

	/// <summary>
	/// Gets the average size per cached item in bytes.
	/// </summary>
	/// <value>The average size per cached item in bytes.</value>
	public long AverageSizeBytes => ItemCount > 0 ? TotalSizeBytes / ItemCount : 0;

	/// <summary>
	/// Gets the number of evictions that have occurred.
	/// </summary>
	/// <value>The number of evictions that have occurred.</value>
	public long EvictionCount { get; init; }

	/// <summary>
	/// Gets the average time to retrieve items from cache in milliseconds.
	/// </summary>
	/// <value>The average time to retrieve items from cache in milliseconds.</value>
	public double AverageGetTimeMs { get; init; }

	/// <summary>
	/// Gets the average time to store items in cache in milliseconds.
	/// </summary>
	/// <value>The average time to store items in cache in milliseconds.</value>
	public double AverageSetTimeMs { get; init; }

	/// <summary>
	/// Gets the 95th percentile get time in milliseconds.
	/// </summary>
	/// <value>The 95th percentile get time in milliseconds.</value>
	public double P95GetTimeMs { get; init; }

	/// <summary>
	/// Gets the 99th percentile get time in milliseconds.
	/// </summary>
	/// <value>The 99th percentile get time in milliseconds.</value>
	public double P99GetTimeMs { get; init; }

	/// <summary>
	/// Gets the maximum get time in milliseconds.
	/// </summary>
	/// <value>The maximum get time in milliseconds.</value>
	public double MaxGetTimeMs { get; init; }

	/// <summary>
	/// Gets the memory pressure level (0-100).
	/// </summary>
	/// <value>The memory pressure level (0-100).</value>
	public int MemoryPressure { get; init; }

	/// <summary>
	/// Gets the CPU usage percentage for cache operations.
	/// </summary>
	/// <value>The CPU usage percentage for cache operations.</value>
	public double CpuUsagePercent { get; init; }

	/// <summary>
	/// Gets the number of concurrent operations.
	/// </summary>
	/// <value>The number of concurrent operations.</value>
	public int ConcurrentOperations { get; init; }

	/// <summary>
	/// Gets the number of pending operations in queue.
	/// </summary>
	/// <value>The number of pending operations in queue.</value>
	public int PendingOperations { get; init; }

	/// <summary>
	/// Gets the cache throughput in operations per second.
	/// </summary>
	/// <value>The cache throughput in operations per second.</value>
	public double ThroughputOpsPerSecond { get; init; }

	/// <summary>
	/// Gets error counts by error type.
	/// </summary>
	/// <value>Error counts by error type.</value>
	public IReadOnlyDictionary<string, long> ErrorCounts { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the total error count.
	/// </summary>
	/// <value>The total error count.</value>
	public long TotalErrors { get; init; }

	/// <summary>
	/// Gets the error rate (errors per request).
	/// </summary>
	/// <value>The error rate (errors per request).</value>
	public double ErrorRate => TotalRequests > 0 ? (double)TotalErrors / TotalRequests : 0;

	/// <summary>
	/// Gets cache-specific metrics.
	/// </summary>
	/// <value>Cache-specific metrics.</value>
	public IReadOnlyDictionary<string, object> CustomMetrics { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the duration over which these metrics were collected.
	/// </summary>
	/// <value>The duration over which these metrics were collected.</value>
	public TimeSpan CollectionDuration { get; init; }

	/// <summary>
	/// Gets a value indicating whether the cache is healthy based on these metrics.
	/// </summary>
	/// <value><see langword="true"/> if the cache is healthy based on these metrics; otherwise, <see langword="false"/>.</value>
	public bool IsHealthy { get; init; } = true;

	/// <summary>
	/// Gets health warnings if any.
	/// </summary>
	/// <value>Health warnings if any.</value>
	public IReadOnlyList<string> HealthWarnings { get; init; } = [];

	/// <summary>
	/// Creates a summary string of the performance snapshot.
	/// </summary>
	/// <returns> A formatted summary of key metrics. </returns>
	public override string ToString() =>
		$"CachePerformanceSnapshot[{Timestamp:O}]: " +
		$"HitRatio={HitRatio:P2}, " +
		$"Items={ItemCount:N0}, " +
		$"Size={TotalSizeBytes / (1024.0 * 1024.0):F2}MB, " +
		$"AvgGetTime={AverageGetTimeMs:F2}ms, " +
		$"Throughput={ThroughputOpsPerSecond:F1}ops/s, " +
		$"Errors={ErrorRate:P2}";
}
