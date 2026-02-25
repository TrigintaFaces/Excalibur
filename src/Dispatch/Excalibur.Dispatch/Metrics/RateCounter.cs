// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Thread-safe counter for tracking counts and rates.
/// </summary>
public sealed class RateCounter : IMetric
{
	private long _value;
	private long _lastValue;
	private DateTimeOffset _lastSnapshot;

	/// <summary>
	/// Initializes a new instance of the <see cref="RateCounter" /> class.
	/// </summary>
	public RateCounter()
	{
		LastReset = DateTimeOffset.UtcNow;
		_lastSnapshot = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RateCounter" /> class.
	/// </summary>
	/// <param name="meter"> The meter (unused, for compatibility). </param>
	/// <param name="name"> The name of the counter (unused, for compatibility). </param>
	/// <param name="unit"> The unit of measurement (unused, for compatibility). </param>
	/// <param name="description"> The description (unused, for compatibility). </param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Public API compatibility constructor - parameters reserved for future OpenTelemetry Meter integration")]
	public RateCounter(object? meter, string name, string? unit, string? description)
	{
		LastReset = DateTimeOffset.UtcNow;
		_lastSnapshot = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Gets the metric metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public MetricMetadata? Metadata { get; }

	/// <summary>
	/// Gets the current value of the counter.
	/// </summary>
	/// <value>
	/// The current value of the counter.
	/// </value>
	public long Value => Interlocked.Read(ref _value);

	/// <summary>
	/// Gets the time when the counter was last reset.
	/// </summary>
	/// <value>The current <see cref="LastReset"/> value.</value>
	public DateTimeOffset LastReset { get; private set; }

	/// <summary>
	/// Increments the counter by 1.
	/// </summary>
	/// <returns> The new value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Increment() => Interlocked.Increment(ref _value);

	/// <summary>
	/// Increments the counter by a specific amount.
	/// </summary>
	/// <param name="amount"> The amount to increment by. </param>
	/// <returns> The new value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long IncrementBy(long amount) => Interlocked.Add(ref _value, amount);

	/// <summary>
	/// Decrements the counter by 1.
	/// </summary>
	/// <returns> The new value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Decrement() => Interlocked.Decrement(ref _value);

	/// <summary>
	/// Decrements the counter by a specific amount.
	/// </summary>
	/// <param name="amount"> The amount to decrement by. </param>
	/// <returns> The new value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long DecrementBy(long amount) => Interlocked.Add(ref _value, -amount);

	/// <summary>
	/// Sets the counter to a specific value.
	/// </summary>
	/// <param name="newValue"> The new value. </param>
	/// <returns> The previous value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public long Set(long newValue) => Interlocked.Exchange(ref _value, newValue);

	/// <summary>
	/// Resets the counter to zero.
	/// </summary>
	/// <returns> The value before reset. </returns>
	public long Reset()
	{
		LastReset = DateTimeOffset.UtcNow;
		_lastValue = 0;
		return Interlocked.Exchange(ref _value, 0);
	}

	/// <summary>
	/// Gets the rate of change per second since the last snapshot.
	/// </summary>
	/// <returns> The rate per second. </returns>
	public double GetRate()
	{
		var now = DateTimeOffset.UtcNow;
		var currentValue = Value;

		var timeDiff = (now - _lastSnapshot).TotalSeconds;
		if (timeDiff <= 0)
		{
			return 0;
		}

		var valueDiff = currentValue - _lastValue;
		var rate = valueDiff / timeDiff;

		_lastSnapshot = now;
		_lastValue = currentValue;

		return rate;
	}

	/// <summary>
	/// Gets the average rate since the last reset.
	/// </summary>
	/// <returns> The average rate per second. </returns>
	public double GetAverageRate()
	{
		var timeSinceReset = (DateTimeOffset.UtcNow - LastReset).TotalSeconds;
		if (timeSinceReset <= 0)
		{
			return 0;
		}

		return Value / timeSinceReset;
	}

	/// <summary>
	/// Gets a snapshot of the counter state.
	/// </summary>
	/// <returns> A snapshot of the counter. </returns>
	public RateCounterSnapshot GetSnapshot()
	{
		var now = DateTimeOffset.UtcNow;
		var currentValue = Value;

		return new RateCounterSnapshot
		{
			Value = currentValue,
			Timestamp = now,
			LastReset = LastReset,
			TimeSinceReset = now - LastReset,
			Rate = GetRate(),
			AverageRate = GetAverageRate(),
		};
	}

	/// <summary>
	/// Adds another counter's value to this counter.
	/// </summary>
	/// <param name="other"> The counter to add. </param>
	public void Add(RateCounter other)
	{
		ArgumentNullException.ThrowIfNull(other);

		_ = IncrementBy(other.Value);
	}

	/// <summary>
	/// Creates a string representation of the counter.
	/// </summary>
	/// <returns> A string representation. </returns>
	public override string ToString() => $"Counter[Value={Value}, Rate={GetRate():F2}/s, AvgRate={GetAverageRate():F2}/s]";
}
