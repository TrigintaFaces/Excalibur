// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Comprehensive statistics for the writer pool.
/// </summary>
public sealed class PoolStatistics
{
	/// <summary>
	/// Gets current number of writers in the pool.
	/// </summary>
	/// <value>The current <see cref="CurrentPoolSize"/> value.</value>
	public int CurrentPoolSize { get; init; }

	/// <summary>
	/// Gets maximum allowed pool size.
	/// </summary>
	/// <value>The current <see cref="MaxPoolSize"/> value.</value>
	public int MaxPoolSize { get; init; }

	/// <summary>
	/// Gets peak pool size reached.
	/// </summary>
	/// <value>The current <see cref="PeakPoolSize"/> value.</value>
	public int PeakPoolSize { get; init; }

	/// <summary>
	/// Gets total number of writers rented.
	/// </summary>
	/// <value>The current <see cref="TotalRented"/> value.</value>
	public long TotalRented { get; init; }

	/// <summary>
	/// Gets total number of writers returned.
	/// </summary>
	/// <value>The current <see cref="TotalReturned"/> value.</value>
	public long TotalReturned { get; init; }

	/// <summary>
	/// Gets number of successful thread-local cache hits.
	/// </summary>
	/// <value>The current <see cref="ThreadLocalHits"/> value.</value>
	public long ThreadLocalHits { get; init; }

	/// <summary>
	/// Gets number of thread-local cache misses.
	/// </summary>
	/// <value>The current <see cref="ThreadLocalMisses"/> value.</value>
	public long ThreadLocalMisses { get; init; }

	/// <summary>
	/// Gets thread-local cache hit rate (0.0 to 1.0).
	/// </summary>
	/// <value>The current <see cref="ThreadLocalHitRate"/> value.</value>
	public double ThreadLocalHitRate { get; init; }

	/// <summary>
	/// Gets number of times options didn't match in the pool.
	/// </summary>
	/// <value>The current <see cref="OptionMismatches"/> value.</value>
	public long OptionMismatches { get; init; }

	/// <summary>
	/// Gets number of times the pool was expanded.
	/// </summary>
	/// <value>The current <see cref="PoolExpansions"/> value.</value>
	public long PoolExpansions { get; init; }

	/// <summary>
	/// Gets number of times the pool was contracted.
	/// </summary>
	/// <value>The current <see cref="PoolContractions"/> value.</value>
	public long PoolContractions { get; init; }

	/// <summary>
	/// Gets estimated number of active writers (rented but not returned).
	/// </summary>
	/// <value>The current <see cref="ActiveWriters"/> value.</value>
	public long ActiveWriters { get; init; }
}
