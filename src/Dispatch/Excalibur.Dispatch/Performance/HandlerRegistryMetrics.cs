// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Handler registry performance metrics.
/// </summary>
public sealed record HandlerRegistryMetrics
{
	/// <summary>
	/// Gets the total number of handler lookups performed.
	/// </summary>
	/// <value>The total lookup attempts captured.</value>
	public required int TotalLookups { get; init; }

	/// <summary>
	/// Gets the total time spent on handler lookups.
	/// </summary>
	/// <value>The cumulative lookup duration.</value>
	public required TimeSpan TotalLookupTime { get; init; }

	/// <summary>
	/// Gets the average time per handler lookup.
	/// </summary>
	/// <value>The mean time taken per lookup.</value>
	public required TimeSpan AverageLookupTime { get; init; }

	/// <summary>
	/// Gets the average number of handlers found per lookup.
	/// </summary>
	/// <value>The average handler count resolved per lookup.</value>
	public required double AverageHandlersPerLookup { get; init; }

	/// <summary>
	/// Gets the number of cache hits during lookups.
	/// </summary>
	/// <value>The number of times a lookup used cached metadata.</value>
	public required int CacheHits { get; init; }

	/// <summary>
	/// Gets the number of cache misses during lookups.
	/// </summary>
	/// <value>The number of lookups that required recomputation.</value>
	public required int CacheMisses { get; init; }

	/// <summary>
	/// Gets the cache hit rate as a percentage (0.0 to 1.0).
	/// </summary>
	/// <value>The ratio of cache hits to total lookups.</value>
	public required double CacheHitRate { get; init; }
}
