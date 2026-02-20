// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Thread-safe gauge for tracking current values.
/// </summary>
public sealed class ValueGauge : IMetric
{
	private long _value;
	private long _count;
	private double _min = double.MaxValue;
	private double _max = double.MinValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValueGauge" /> class.
	/// </summary>
	public ValueGauge() => LastUpdated = DateTimeOffset.UtcNow;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValueGauge" /> class with metadata.
	/// </summary>
	public ValueGauge(MetricMetadata metadata)
	{
		Metadata = metadata;
		LastUpdated = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Gets the metric metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public MetricMetadata? Metadata { get; }

	/// <summary>
	/// Gets the current value of the gauge.
	/// </summary>
	/// <value>
	/// The current value of the gauge.
	/// </value>
	public long Value => Interlocked.Read(ref _value);

	/// <summary>
	/// Gets when the gauge was last updated.
	/// </summary>
	/// <value>The current <see cref="LastUpdated"/> value.</value>
	public DateTimeOffset LastUpdated { get; private set; }

	/// <summary>
	/// Sets the gauge to a specific value.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Set(long value)
	{
		_ = Interlocked.Exchange(ref _value, value);
		_ = Interlocked.Increment(ref _count);
		LastUpdated = DateTimeOffset.UtcNow;

		var doubleValue = (double)value;
		if (doubleValue < _min)
		{
			_min = doubleValue;
		}

		if (doubleValue > _max)
		{
			_max = doubleValue;
		}
	}

	/// <summary>
	/// Increments the gauge by 1.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Increment()
	{
		LastUpdated = DateTimeOffset.UtcNow;
		return Interlocked.Increment(ref _value);
	}

	/// <summary>
	/// Increments the gauge by a specific amount.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long IncrementBy(long amount)
	{
		LastUpdated = DateTimeOffset.UtcNow;
		return Interlocked.Add(ref _value, amount);
	}

	/// <summary>
	/// Decrements the gauge by 1.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Decrement()
	{
		LastUpdated = DateTimeOffset.UtcNow;
		return Interlocked.Decrement(ref _value);
	}

	/// <summary>
	/// Decrements the gauge by a specific amount.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long DecrementBy(long amount)
	{
		LastUpdated = DateTimeOffset.UtcNow;
		return Interlocked.Add(ref _value, -amount);
	}

	/// <summary>
	/// Resets the gauge to zero.
	/// </summary>
	public void Reset()
	{
		_ = Interlocked.Exchange(ref _value, 0);
		LastUpdated = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Creates a string representation of the gauge.
	/// </summary>
	public override string ToString() => $"Gauge[Value={Value}, LastUpdated={LastUpdated:o}]";

	/// <summary>
	/// Gets a snapshot of the gauge state.
	/// </summary>
	internal GaugeSnapshot GetSnapshot() =>
		new()
		{
			Value = Value,
			LastUpdated = LastUpdated,
			Timestamp = LastUpdated,
			Count = Interlocked.Read(ref _count),
			Min = _min == double.MaxValue ? 0 : _min,
			Max = _max == double.MinValue ? 0 : _max,
		};

	/// <summary>
	/// Snapshot of gauge state.
	/// </summary>
	internal sealed class GaugeSnapshot
	{
		/// <summary>
		/// Gets or sets the gauge value.
		/// </summary>
		/// <value>The current <see cref="Value"/> value.</value>
		public long Value { get; set; }

		/// <summary>
		/// Gets or sets when the gauge was last updated.
		/// </summary>
		/// <value>The current <see cref="LastUpdated"/> value.</value>
		public DateTimeOffset LastUpdated { get; set; }

		/// <summary>
		/// Gets or sets the timestamp.
		/// </summary>
		/// <value>The current <see cref="Timestamp"/> value.</value>
		public DateTimeOffset Timestamp { get; set; }

		/// <summary>
		/// Gets or sets the count of values.
		/// </summary>
		/// <value>The current <see cref="Count"/> value.</value>
		public long Count { get; set; }

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
	}
}
