// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Configuration specific to memory caching.
/// </summary>
public sealed class MemoryCacheConfiguration
{
	/// <summary>
	/// Gets or sets the size limit for the memory cache. When set, items must specify their size.
	/// </summary>
	/// <value>The size limit for the memory cache.</value>
	public long? SizeLimit { get; set; }

	/// <summary>
	/// Gets or sets the percentage of memory to free when the cache size limit is exceeded. Default is 5%.
	/// </summary>
	/// <value>The percentage of memory to free when the cache size limit is exceeded.</value>
	public double CompactionPercentage { get; set; } = 0.05;

	/// <summary>
	/// Gets or sets how often expired items are removed from the cache. Default is 1 minute.
	/// </summary>
	/// <value>How often expired items are removed from the cache.</value>
	public TimeSpan ExpirationScanFrequency { get; set; } = TimeSpan.FromMinutes(1);
}
