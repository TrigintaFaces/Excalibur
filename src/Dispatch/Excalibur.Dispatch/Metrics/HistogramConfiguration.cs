// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Configuration for histogram buckets.
/// </summary>
public class HistogramConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HistogramConfiguration" /> class with the specified bucket boundaries. Bucket
	/// boundaries are automatically sorted to ensure proper histogram operation.
	/// </summary>
	/// <param name="buckets"> The bucket boundary values for the histogram. Must contain at least one value. </param>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="buckets" /> is null or empty. </exception>
	public HistogramConfiguration(params double[] buckets)
	{
		if (buckets == null || buckets.Length == 0)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_AtLeastOneBucketBoundaryIsRequired, nameof(buckets));
		}

		// Ensure buckets are sorted
		Buckets = new double[buckets.Length];
		Array.Copy(buckets, Buckets, buckets.Length);
		Array.Sort(Buckets);
	}

	/// <summary>
	/// Gets default buckets for response time measurements (in seconds).
	/// </summary>
	/// <value>
	/// Default buckets for response time measurements (in seconds).
	/// </value>
	public static HistogramConfiguration DefaultLatency => new(
		0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10);

	/// <summary>
	/// Gets default buckets for request size measurements (in bytes).
	/// </summary>
	/// <value>
	/// Default buckets for request size measurements (in bytes).
	/// </value>
	public static HistogramConfiguration DefaultSize => Exponential(100, 2, 10);

	/// <summary>
	/// Gets the sorted array of bucket boundaries for the histogram. These boundaries define the ranges for categorizing measured values.
	/// </summary>
	/// <value> An array of bucket boundary values in ascending order. </value>
	public double[] Buckets { get; }

	/// <summary>
	/// Creates exponential buckets: start * factor^i for i in 0..count-1.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	public static HistogramConfiguration Exponential(double start, double factor, int count)
	{
		if (start <= 0)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_StartMustBePositive, nameof(start));
		}

		if (factor <= 1)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_FactorMustBeGreaterThanOne, nameof(factor));
		}

		if (count <= 0)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_CountMustBePositive, nameof(count));
		}

		var buckets = new double[count];
		buckets[0] = start;
		for (var i = 1; i < count; i++)
		{
			buckets[i] = buckets[i - 1] * factor;
		}

		return new HistogramConfiguration(buckets);
	}

	/// <summary>
	/// Creates linear buckets: start + width*i for i in 0..count-1.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	public static HistogramConfiguration Linear(double start, double width, int count)
	{
		if (width <= 0)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_WidthMustBePositive, nameof(width));
		}

		if (count <= 0)
		{
			throw new ArgumentException(Resources.HistogramConfiguration_CountMustBePositive, nameof(count));
		}

		var buckets = new double[count];
		for (var i = 0; i < count; i++)
		{
			buckets[i] = start + (width * i);
		}

		return new HistogramConfiguration(buckets);
	}
}
