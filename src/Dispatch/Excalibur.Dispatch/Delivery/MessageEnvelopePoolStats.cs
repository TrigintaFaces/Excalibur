// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Statistics for message envelope pool.
/// </summary>
public sealed class MessageEnvelopePoolStats
{
	/// <summary>
	/// Gets or sets the total number of rentals.
	/// </summary>
	/// <value>The current <see cref="TotalRentals"/> value.</value>
	public long TotalRentals { get; set; }

	/// <summary>
	/// Gets or sets the total number of returns.
	/// </summary>
	/// <value>The current <see cref="TotalReturns"/> value.</value>
	public long TotalReturns { get; set; }

	/// <summary>
	/// Gets or sets the number of pool hits.
	/// </summary>
	/// <value>The current <see cref="PoolHits"/> value.</value>
	public long PoolHits { get; set; }

	/// <summary>
	/// Gets or sets the number of pool misses.
	/// </summary>
	/// <value>The current <see cref="PoolMisses"/> value.</value>
	public long PoolMisses { get; set; }

	/// <summary>
	/// Gets or sets the hit rate.
	/// </summary>
	/// <value>The current <see cref="HitRate"/> value.</value>
	public double HitRate { get; set; }

	/// <summary>
	/// Gets or sets thread-local statistics.
	/// </summary>
	/// <value>The current <see cref="ThreadLocalStats"/> value.</value>
	public ThreadLocalPoolStats[] ThreadLocalStats { get; set; } = [];
}
