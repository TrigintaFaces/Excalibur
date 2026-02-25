// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Thread-safe histogram for tracking value distributions.
/// </summary>
public sealed class ValueHistogram : IMetric
{
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private readonly List<double> _values = [];
	private long _count;
	private double _sum;
	private double _min = double.MaxValue;
	private double _max = double.MinValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValueHistogram" /> class.
	/// </summary>
	public ValueHistogram()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ValueHistogram" /> class with metadata and configuration.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Configuration parameter reserved for future histogram bucket customization and statistical algorithm options")]
	public ValueHistogram(MetricMetadata metadata, HistogramConfiguration configuration)
	{
		Metadata = metadata;
	}

	/// <summary>
	/// Gets the metric metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public MetricMetadata? Metadata { get; }

	/// <summary>
	/// Gets the current count of recorded values.
	/// </summary>
	/// <value>
	/// The current count of recorded values.
	/// </value>
	public long Count => Interlocked.Read(ref _count);

	/// <summary>
	/// Gets the mean of recorded values.
	/// </summary>
	/// <value>
	/// The mean of recorded values.
	/// </value>
	public double Mean
	{
		get
		{
			lock (_lock)
			{
				return _count > 0 ? _sum / _count : 0;
			}
		}
	}

	/// <summary>
	/// Gets the minimum recorded value.
	/// </summary>
	/// <value>
	/// The minimum recorded value.
	/// </value>
	public double Min
	{
		get
		{
			lock (_lock)
			{
				return _count > 0 ? _min : 0;
			}
		}
	}

	/// <summary>
	/// Gets the maximum recorded value.
	/// </summary>
	/// <value>
	/// The maximum recorded value.
	/// </value>
	public double Max
	{
		get
		{
			lock (_lock)
			{
				return _count > 0 ? _max : 0;
			}
		}
	}

	/// <summary>
	/// Records a value in the histogram.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Record(double value)
	{
		lock (_lock)
		{
			_values.Add(value);
			_count++;
			_sum += value;
			_min = Math.Min(_min, value);
			_max = Math.Max(_max, value);
		}
	}

	/// <summary>
	/// Records a duration in milliseconds.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RecordMilliseconds(double milliseconds) => Record(milliseconds);

	/// <summary>
	/// Calculates a percentile value.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public double GetPercentile(double percentile)
	{
		if (percentile is < 0 or > 100)
		{
			throw new ArgumentOutOfRangeException(nameof(percentile), ErrorMessages.PercentileMustBeBetween0And100);
		}

		lock (_lock)
		{
			if (_values.Count == 0)
			{
				return 0;
			}

			// AD-251-4: Avoid ToList() allocation - sort in-place with array copy
			var sortedValues = _values.ToArray();
			Array.Sort(sortedValues);
			var index = (int)Math.Ceiling(percentile / 100 * sortedValues.Length) - 1;
			index = Math.Max(0, Math.Min(index, sortedValues.Length - 1));
			return sortedValues[index];
		}
	}

	/// <summary>
	/// Resets the histogram.
	/// </summary>
	public void Reset()
	{
		lock (_lock)
		{
			_values.Clear();
			_count = 0;
			_sum = 0;
			_min = double.MaxValue;
			_max = double.MinValue;
		}
	}

	/// <summary>
	/// Gets a snapshot of the histogram state.
	/// </summary>
	public HistogramSnapshot GetSnapshot()
	{
		// AD-251-4: Single lock acquisition - avoid re-entrant locking from GetPercentile() calls
		lock (_lock)
		{
			return new HistogramSnapshot
			{
				Count = _count,
				Sum = _sum,
				Mean = _count > 0 ? _sum / _count : 0,
				Min = _count > 0 ? _min : 0,
				Max = _count > 0 ? _max : 0,
				P50 = GetPercentileUnsafe(50),
				P75 = GetPercentileUnsafe(75),
				P95 = GetPercentileUnsafe(95),
				P99 = GetPercentileUnsafe(99),
			};
		}
	}

	/// <summary>
	/// Internal percentile calculation without lock - caller must hold _lock.
	/// </summary>
	private double GetPercentileUnsafe(double percentile)
	{
		if (_values.Count == 0)
		{
			return 0;
		}

		// AD-251-4: Avoid ToList() allocation - sort in-place with array copy
		var sortedValues = _values.ToArray();
		Array.Sort(sortedValues);
		var index = (int)Math.Ceiling(percentile / 100 * sortedValues.Length) - 1;
		index = Math.Max(0, Math.Min(index, sortedValues.Length - 1));
		return sortedValues[index];
	}
}
