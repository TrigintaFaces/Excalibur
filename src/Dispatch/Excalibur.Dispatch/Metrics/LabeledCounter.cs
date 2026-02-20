// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// High-performance counter with label support.
/// </summary>
public sealed class LabeledCounter : IMetric
{
	private readonly ConcurrentDictionary<LabelSet, long> _counters;

	/// <summary>
	/// Initializes a new instance of the <see cref="LabeledCounter" /> class with the specified metadata. Creates a high-performance
	/// counter that supports multiple label combinations for categorization.
	/// </summary>
	/// <param name="metadata"> The metadata describing the counter metric. Must specify a counter type. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="metadata" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when metadata does not specify a counter type. </exception>
	public LabeledCounter(MetricMetadata metadata)
	{
		Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
		if (metadata.Type != MetricType.Counter)
		{
			throw new ArgumentException(Resources.LabeledCounter_MetadataMustBeForCounterType, nameof(metadata));
		}

		_counters = new ConcurrentDictionary<LabelSet, long>();
	}

	/// <summary>
	/// Gets the metadata describing this counter metric. Includes name, description, units, and other metric configuration information.
	/// </summary>
	/// <value> The metric metadata for this counter. </value>
	public MetricMetadata Metadata { get; }

	/// <summary>
	/// Gets the number of distinct label combinations currently tracked by this counter.
	/// </summary>
	/// <value>The current <see cref="LabelCount"/> value.</value>
	public int LabelCount => _counters.Count;

	/// <summary>
	/// Increments the counter by the specified value for the given label combination.
	/// </summary>
	/// <param name="value"> The amount to increment the counter by. Must be non-negative. </param>
	/// <param name="labelValues"> The label values that identify the specific counter instance. </param>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is negative. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Increment(long value = 1, params string[] labelValues)
	{
		if (value < 0)
		{
			throw new ArgumentException(Resources.LabeledCounter_CounterCanOnlyIncrease, nameof(value));
		}

		var labelSet = new LabelSet(labelValues);
		_counters.AddOrUpdate(labelSet, value, (_, existing) => existing + value);
	}

	/// <summary>
	/// Gets snapshots of all counter values for the different label combinations.
	/// </summary>
	/// <returns> An array of metric snapshots representing the current counter values. </returns>
	public MetricSnapshot[] GetSnapshots()
	{
		var snapshots = new MetricSnapshot[_counters.Count];
		var index = 0;
		var timestamp = DateTimeOffset.UtcNow.Ticks;

		foreach (var kvp in _counters)
		{
			var value = kvp.Value;
			snapshots[index++] = new MetricSnapshot(
				Metadata.MetricId,
				MetricType.Counter,
				timestamp,
				value,
				kvp.Key.GetHashCode(), // Use hash as label set ID
				count: 1,
				sum: value);
		}

		return snapshots;
	}

	/// <summary>
	/// Resets all counter values to zero for all label combinations.
	/// </summary>
	public void Reset()
	{
		foreach (var key in _counters.Keys)
		{
			_counters[key] = 0;
		}
	}
}
