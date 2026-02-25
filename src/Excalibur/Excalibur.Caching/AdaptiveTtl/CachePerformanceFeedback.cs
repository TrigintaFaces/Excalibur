// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Performance feedback for updating adaptive strategies.
/// </summary>
public sealed class CachePerformanceFeedback
{
	/// <summary>
	/// Gets or sets the cache key.
	/// </summary>
	/// <value> The cache key associated with this feedback. </value>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether it was a cache hit.
	/// </summary>
	/// <value> <see langword="true" /> if the cache was hit; otherwise, <see langword="false" />. </value>
	public bool IsHit { get; set; }

	/// <summary>
	/// Gets or sets the response time.
	/// </summary>
	/// <value> The time taken to respond to the cache operation. </value>
	public TimeSpan ResponseTime { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the operation.
	/// </summary>
	/// <value> The timestamp when the cache operation occurred. </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the current TTL value.
	/// </summary>
	/// <value> The current time-to-live value for the cached item. </value>
	public TimeSpan CurrentTtl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the content was stale.
	/// </summary>
	/// <value> <see langword="true" /> if the content was stale; otherwise, <see langword="false" />. </value>
	public bool WasStale { get; set; }
}
