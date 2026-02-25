// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pools;

/// <summary>
/// Pool statistics for monitoring and optimization.
/// </summary>
public sealed class PoolStatistics
{
	/// <summary>
	/// Gets or sets the total number of rental operations performed by the pool since initialization.
	/// </summary>
	/// <value>The current <see cref="TotalRentals"/> value.</value>
	public long TotalRentals { get; set; }

	/// <summary>
	/// Gets or sets the total number of return operations performed by the pool since initialization.
	/// </summary>
	/// <value>The current <see cref="TotalReturns"/> value.</value>
	public long TotalReturns { get; set; }

	/// <summary>
	/// Gets or sets the current number of items available in the pool.
	/// </summary>
	/// <value>The current <see cref="CurrentPoolSize"/> value.</value>
	public int CurrentPoolSize { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of items that the pool can contain.
	/// </summary>
	/// <value>The current <see cref="MaxPoolSize"/> value.</value>
	public int MaxPoolSize { get; set; }
}
