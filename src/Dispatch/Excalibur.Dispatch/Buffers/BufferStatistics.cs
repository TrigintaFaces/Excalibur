// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Statistics about buffer usage.
/// </summary>
public sealed class BufferStatistics
{
	/// <summary>
	/// Gets or sets the total number of buffers that have been rented.
	/// </summary>
	/// <value>The current <see cref="TotalRented"/> value.</value>
	public long TotalRented { get; set; }

	/// <summary>
	/// Gets or sets the total number of buffers that have been returned.
	/// </summary>
	/// <value>The current <see cref="TotalReturned"/> value.</value>
	public long TotalReturned { get; set; }

	/// <summary>
	/// Gets or sets the current number of outstanding buffers (rented but not yet returned).
	/// </summary>
	/// <value>The current <see cref="OutstandingBuffers"/> value.</value>
	public long OutstandingBuffers { get; set; }

	/// <summary>
	/// Gets the distribution of buffer sizes showing count of each size used.
	/// </summary>
	/// <value>
	/// The distribution of buffer sizes showing count of each size used.
	/// </value>
	public IDictionary<int, long> SizeDistribution { get; init; } = new Dictionary<int, long>();
}
