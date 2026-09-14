// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Configuration options for the Dispatch caching system. Supports memory, distributed, and hybrid caching modes.
/// </summary>
public sealed class CacheOptions
{
	private CacheMode _cacheMode = CacheMode.Hybrid;
	private bool _cacheModeExplicitlySet;

	/// <summary>
	/// Gets or sets a value indicating whether caching is enabled. Default is false, must be explicitly enabled.
	/// </summary>
	/// <value><see langword="true"/> if caching is enabled; otherwise, <see langword="false"/>.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the caching mode to use. Determines whether to use memory, distributed, or hybrid caching.
	/// </summary>
	/// <value>The caching mode to use.</value>
	public CacheMode CacheMode
	{
		get => _cacheMode;
		set
		{
			_cacheMode = value;
			_cacheModeExplicitlySet = true;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether to use distributed cache. This is a convenience property that sets CacheMode to Distributed
	/// when true.
	/// </summary>
	/// <value><see langword="true"/> if distributed cache should be used; otherwise, <see langword="false"/>.</value>
	public bool UseDistributedCache
	{
		get => CacheMode is CacheMode.Distributed or CacheMode.Hybrid;
		set
		{
			if (value)
			{
				// Preserve an explicit Hybrid assignment during option copying/binding.
				// This avoids silently downgrading Hybrid -> Distributed.
				if (_cacheModeExplicitlySet && CacheMode == CacheMode.Hybrid)
				{
					return;
				}

				CacheMode = CacheMode.Distributed;
				return;
			}

			CacheMode = CacheMode.Memory;
		}
	}

	/// <summary>
	/// Gets or sets the default tags to apply to all cached items. Tags enable bulk invalidation of related cache entries.
	/// </summary>
	/// <value>The default tags to apply to all cached items.</value>
	public string[] DefaultTags { get; set; } = [];

	/// <summary>
	/// Gets or sets the maximum number of distinct tags tracked by the in-memory tag tracker.
	/// Default is 10,000.
	/// </summary>
	/// <value>The maximum number of tracked tag version stamps.</value>
	public int TagTrackerCapacity { get; set; } = 10_000;

	/// <summary>
	/// Gets or sets how long a resolved tag version stamp is trusted before a tag tracker re-checks
	/// its backend for a newer one. Default is 5 seconds. This interval is the bound on how long an
	/// invalidation made on one instance can take to be observed by another.
	/// </summary>
	/// <value>The refresh interval for a tag tracker's per-tag version stamp memo.</value>
	public TimeSpan TagStampRefreshInterval { get; set; } = DefaultTagStampRefreshInterval;

	/// <summary>
	/// Gets or sets how long a tag's version stamp record is retained in the distributed cache
	/// backend. Default is 1000 days. Must be strictly greater than <see cref="CacheBehaviorOptions.DefaultExpiration"/>
	/// so that a cache entry can never outlive the tag stamp record it was written against.
	/// </summary>
	/// <value>The retention period for a tag's version stamp record.</value>
	public TimeSpan TagStampLifetime { get; set; } = DefaultTagStampLifetime;

	/// <summary>
	/// The default value of <see cref="TagStampRefreshInterval"/>.
	/// </summary>
	internal static readonly TimeSpan DefaultTagStampRefreshInterval = TimeSpan.FromSeconds(5);

	/// <summary>
	/// The default value of <see cref="TagStampLifetime"/>.
	/// </summary>
	internal static readonly TimeSpan DefaultTagStampLifetime = TimeSpan.FromDays(1000);

	/// <summary>
	/// Gets or sets the global cache policy to apply to all cacheable operations. Can be overridden per operation.
	/// </summary>
	/// <value>The global cache policy to apply to all cacheable operations.</value>
	public IResultCachePolicy? GlobalPolicy { get; set; }

	/// <summary>
	/// Gets or sets the cache key builder used to generate cache keys. If not specified, DefaultCacheKeyBuilder is used.
	/// </summary>
	/// <value>The cache key builder used to generate cache keys.</value>
	public ICacheKeyBuilder? CacheKeyBuilder { get; set; }

	/// <summary>
	/// Gets or sets distributed cache specific options. Only used when CacheMode is Distributed or Hybrid.
	/// </summary>
	/// <value>Distributed cache specific options.</value>
	public DistributedCacheOptions Distributed { get; set; } = new();

	/// <summary>
	/// Gets or sets resilience configuration for cache operations including circuit breaker settings.
	/// </summary>
	/// <value>Resilience configuration for cache operations.</value>
	public CacheResilienceOptions Resilience { get; set; } = new();

	/// <summary>
	/// Gets or sets expiration and behavior configuration for cache operations.
	/// </summary>
	/// <value>Expiration and behavior configuration for cache operations.</value>
	public CacheBehaviorOptions Behavior { get; set; } = new();
}

/// <summary>
/// Expiration, timeout, and behavior configuration for cache operations.
/// </summary>
public sealed class CacheBehaviorOptions
{
	/// <summary>
	/// Gets or sets the default expiration time for cached items. Default is 10 minutes.
	/// </summary>
	/// <value>The default expiration time for cached items.</value>
	public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the maximum time to wait for a single distributed (L2) cache backend call before the
	/// call is abandoned. Default is 200 milliseconds.
	/// </summary>
	/// <value>The maximum time to wait for one distributed cache backend call.</value>
	/// <remarks>
	/// <para>
	/// This bounds backend I/O only. It does not bound handler execution: a cached operation runs the
	/// handler, and a handler is expected to take as long as its work takes. A cache whose deadline could
	/// expire while the handler runs would abandon the shared in-flight operation that collapses
	/// concurrent callers into one execution, so a slow handler would lose stampede protection precisely
	/// where it is most valuable.
	/// </para>
	/// <para>
	/// An exceeded deadline is not an error. A read that runs long is reported as a miss and the handler
	/// executes; a write that runs long is dropped and the next request re-populates the entry. Neither
	/// fails the operation being cached.
	/// </para>
	/// </remarks>
	public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

	/// <summary>
	/// Gets or sets the ratio of random jitter applied to cache TTLs. Default is 0.10 (10%).
	/// </summary>
	/// <value>The jitter ratio applied to cache TTLs.</value>
	[Range(0.0, 1.0)]
	public double JitterRatio { get; set; } = 0.10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable cache statistics collection.
	/// </summary>
	/// <value><see langword="true"/> if cache statistics collection should be enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableStatistics { get; set; }
}
