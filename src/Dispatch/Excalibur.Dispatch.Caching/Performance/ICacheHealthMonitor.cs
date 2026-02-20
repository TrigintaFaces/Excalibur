// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Interface for monitoring cache health and performance.
/// </summary>
public interface ICacheHealthMonitor
{
	/// <summary>
	/// Gets the current cache health status.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation, containing the cache health status.</returns>
	Task<CacheHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets basic cache performance metrics.
	/// </summary>
	/// <returns>A snapshot of cache performance metrics.</returns>
	CachePerformanceSnapshot GetPerformanceSnapshot();
}
