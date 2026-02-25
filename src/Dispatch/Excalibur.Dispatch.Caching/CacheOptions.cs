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
	/// Gets or sets memory cache specific options. Only used when CacheMode is Memory or Hybrid.
	/// </summary>
	/// <value>Memory cache specific options.</value>
	public MemoryCacheConfiguration Memory { get; set; } = new();

	/// <summary>
	/// Gets or sets distributed cache specific options. Only used when CacheMode is Distributed or Hybrid.
	/// </summary>
	/// <value>Distributed cache specific options.</value>
	public DistributedCacheConfiguration Distributed { get; set; } = new();

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
	/// Gets or sets a value indicating whether to use sliding expiration. When true, the expiration time is reset each time an item is accessed.
	/// </summary>
	/// <value><see langword="true"/> if sliding expiration should be used; otherwise, <see langword="false"/>.</value>
	public bool UseSlidingExpiration { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum time to wait for cache operations before timeout. Default is 200 milliseconds.
	/// </summary>
	/// <value>The maximum time to wait for cache operations before timeout.</value>
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

	/// <summary>
	/// Gets or sets a value indicating whether to compress cached values in distributed cache.
	/// </summary>
	/// <value><see langword="true"/> if compression should be enabled; otherwise, <see langword="false"/>.</value>
	public bool EnableCompression { get; set; }
}
