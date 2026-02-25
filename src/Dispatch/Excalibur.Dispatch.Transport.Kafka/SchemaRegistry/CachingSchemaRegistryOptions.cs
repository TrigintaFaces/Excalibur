// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for the caching schema registry client decorator.
/// </summary>
public sealed class CachingSchemaRegistryOptions
{
	/// <summary>
	/// Gets or sets the cache duration for schema lookups.
	/// </summary>
	/// <value>The cache duration. Default is 5 minutes.</value>
	public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets whether to cache compatibility check results.
	/// </summary>
	/// <value><see langword="true"/> to cache compatibility results; otherwise, <see langword="false"/>. Default is true.</value>
	public bool CacheCompatibilityResults { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of entries in the cache.
	/// </summary>
	/// <value>The maximum cache size. Default is 1000.</value>
	public int MaxCacheSize { get; set; } = 1000;
}
