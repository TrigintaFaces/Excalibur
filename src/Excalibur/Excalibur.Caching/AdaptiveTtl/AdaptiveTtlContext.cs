// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Context information for calculating adaptive TTL.
/// </summary>
public sealed class AdaptiveTtlContext
{
	/// <summary>
	/// Gets or sets the cache key.
	/// </summary>
	/// <value> The cache key for the item. </value>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the base TTL before adjustments.
	/// </summary>
	/// <value> The base time-to-live value before adaptive adjustments. </value>
	public TimeSpan BaseTtl { get; set; }

	/// <summary>
	/// Gets or sets the current access frequency (requests per minute).
	/// </summary>
	/// <value> The access frequency in requests per minute. </value>
	public double AccessFrequency { get; set; }

	/// <summary>
	/// Gets or sets the recent hit rate (0.0 to 1.0).
	/// </summary>
	/// <value> The cache hit rate as a value between 0.0 and 1.0. </value>
	public double HitRate { get; set; }

	/// <summary>
	/// Gets or sets the last update timestamp.
	/// </summary>
	/// <value> The timestamp of the last update. </value>
	public DateTimeOffset LastUpdate { get; set; }

	/// <summary>
	/// Gets or sets the content size in bytes.
	/// </summary>
	/// <value> The size of the cached content in bytes. </value>
	public long ContentSize { get; set; }

	/// <summary>
	/// Gets or sets the cost of cache miss (e.g., database query time).
	/// </summary>
	/// <value> The time cost incurred when a cache miss occurs. </value>
	public TimeSpan MissCost { get; set; }

	/// <summary>
	/// Gets or sets the current system load (0.0 to 1.0).
	/// </summary>
	/// <value> The current system load as a value between 0.0 and 1.0. </value>
	public double SystemLoad { get; set; }

	/// <summary>
	/// Gets or sets the time of day for temporal adjustments.
	/// </summary>
	/// <value> The current time for temporal TTL adjustments. </value>
	public DateTimeOffset CurrentTime { get; set; }

	/// <summary>
	/// Gets custom metadata for specific strategies.
	/// </summary>
	/// <value> A dictionary of custom metadata for strategy-specific data. </value>
	public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
