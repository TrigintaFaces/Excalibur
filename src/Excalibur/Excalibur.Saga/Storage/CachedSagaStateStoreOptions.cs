// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Storage;

/// <summary>
/// Configuration options for the cached saga state store.
/// </summary>
public sealed class CachedSagaStateStoreOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether caching is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if caching is enabled; otherwise, <see langword="false"/>.
	/// </value>
	public bool EnableCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets the default cache TTL.
	/// </summary>
	/// <value>
	/// The default cache TTL.
	/// </value>
	// R0.8: RangeAttribute with TimeSpan is a well-known validation pattern
#pragma warning disable IL2026
	[Range(typeof(TimeSpan), "00:00:01", "24:00:00")]
#pragma warning restore IL2026
	public TimeSpan DefaultCacheTtl { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the cache TTL for active sagas.
	/// </summary>
	/// <value>
	/// The cache TTL for active sagas.
	/// </value>
	// R0.8: RangeAttribute with TimeSpan is a well-known validation pattern
#pragma warning disable IL2026
	[Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
#pragma warning restore IL2026
	public TimeSpan ActiveSagaCacheTtl { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the cache TTL for completed sagas.
	/// </summary>
	/// <value>
	/// The cache TTL for completed sagas.
	/// </value>
	// R0.8: RangeAttribute with TimeSpan is a well-known validation pattern
#pragma warning disable IL2026
	[Range(typeof(TimeSpan), "00:01:00", "24:00:00")]
#pragma warning restore IL2026
	public TimeSpan CompletedSagaCacheTtl { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets a value indicating whether to invalidate cache on updates.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if cache is invalidated on updates; otherwise, <see langword="false"/>.
	/// </value>
	public bool InvalidateCacheOnUpdate { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use a local memory cache.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if local memory cache is used; otherwise, <see langword="false"/>.
	/// </value>
	public bool UseLocalCache { get; set; } = true;

	/// <summary>
	/// Gets or sets the local cache size limit.
	/// </summary>
	/// <value>
	/// The local cache size limit.
	/// </value>
	[Range(10, 10000)]
	public int LocalCacheSizeLimit { get; set; } = 1000;
}

