// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Histogram snapshot data.
/// </summary>
public sealed class HistogramSnapshot
{
	/// <summary>
	/// Gets or sets the count of values.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public long Count { get; set; }

	/// <summary>
	/// Gets or sets the sum of all values.
	/// </summary>
	/// <value>The current <see cref="Sum"/> value.</value>
	public double Sum { get; set; }

	/// <summary>
	/// Gets or sets the mean value.
	/// </summary>
	/// <value>The current <see cref="Mean"/> value.</value>
	public double Mean { get; set; }

	/// <summary>
	/// Gets or sets the minimum value.
	/// </summary>
	/// <value>The current <see cref="Min"/> value.</value>
	public double Min { get; set; }

	/// <summary>
	/// Gets or sets the maximum value.
	/// </summary>
	/// <value>The current <see cref="Max"/> value.</value>
	public double Max { get; set; }

	/// <summary>
	/// Gets or sets the 50th percentile (median).
	/// </summary>
	/// <value>The current <see cref="P50"/> value.</value>
	public double P50 { get; set; }

	/// <summary>
	/// Gets or sets the 75th percentile.
	/// </summary>
	/// <value>The current <see cref="P75"/> value.</value>
	public double P75 { get; set; }

	/// <summary>
	/// Gets or sets the 95th percentile.
	/// </summary>
	/// <value>The current <see cref="P95"/> value.</value>
	public double P95 { get; set; }

	/// <summary>
	/// Gets or sets the 99th percentile.
	/// </summary>
	/// <value>The current <see cref="P99"/> value.</value>
	public double P99 { get; set; }
}
