// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Defines a strategy for dynamically adjusting cache TTL based on various factors.
/// </summary>
public interface IAdaptiveTtlStrategy
{
	/// <summary>
	/// Calculates the optimal TTL for a cache entry based on current conditions.
	/// </summary>
	/// <param name="context"> The context containing information for TTL calculation. </param>
	/// <returns> The calculated TTL. </returns>
	TimeSpan CalculateTtl(AdaptiveTtlContext context);

	/// <summary>
	/// Updates the strategy based on observed cache performance.
	/// </summary>
	/// <param name="feedback"> Performance feedback from cache operations. </param>
	void UpdateStrategy(CachePerformanceFeedback feedback);

	/// <summary>
	/// Gets the current strategy metrics.
	/// </summary>
	/// <returns> Current metrics for the adaptive strategy. </returns>
	AdaptiveTtlMetrics GetMetrics();
}
