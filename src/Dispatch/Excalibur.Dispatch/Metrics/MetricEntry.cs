// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Zero-allocation metric entry structure for high-performance metrics collection.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MetricEntry(
	long timestampTicks,
	MetricType type,
	int metricId,
	double value,
	int labelSetId = 0) : IEquatable<MetricEntry>
{
	/// <summary>
	/// The timestamp of this metric entry in ticks.
	/// </summary>
	public readonly long TimestampTicks = timestampTicks;

	/// <summary>
	/// The type of metric (Counter, Gauge, etc.).
	/// </summary>
	public readonly MetricType Type = type;

	/// <summary>
	/// The unique identifier for this metric.
	/// </summary>
	public readonly int MetricId = metricId;

	/// <summary>
	/// The metric value.
	/// </summary>
	public readonly double Value = value;

	/// <summary>
	/// The identifier for the label set associated with this metric.
	/// </summary>
	public readonly int LabelSetId = labelSetId;

	/// <summary>
	/// Reserved field for memory alignment (unused).
	/// </summary>
	public readonly int Reserved = 0; // For alignment

	/// <summary>
	/// Gets the size of the MetricEntry structure in bytes.
	/// </summary>
	/// <value>
	/// The size of the MetricEntry structure in bytes.
	/// </value>
	public static int Size => Marshal.SizeOf<MetricEntry>();

	/// <summary>
	/// Determines whether the specified metric entry is equal to the current metric entry.
	/// </summary>
	/// <param name="other"> The metric entry to compare with the current metric entry. </param>
	/// <returns> true if the specified metric entry is equal to the current metric entry; otherwise, false. </returns>
	public bool Equals(MetricEntry other) =>
		TimestampTicks == other.TimestampTicks &&
		Type == other.Type &&
		MetricId == other.MetricId &&
		Value.Equals(other.Value) &&
		LabelSetId == other.LabelSetId &&
		Reserved == other.Reserved;

	/// <summary>
	/// Determines whether the specified object is equal to the current metric entry.
	/// </summary>
	/// <param name="obj"> The object to compare with the current metric entry. </param>
	/// <returns> true if the specified object is equal to the current metric entry; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is MetricEntry other && Equals(other);

	/// <summary>
	/// Returns the hash code for this metric entry.
	/// </summary>
	/// <returns> A hash code for the current metric entry. </returns>
	public override int GetHashCode() => HashCode.Combine(TimestampTicks, Type, MetricId, Value, LabelSetId, Reserved);

	/// <summary>
	/// Determines whether two metric entries are equal.
	/// </summary>
	/// <param name="left"> The first metric entry to compare. </param>
	/// <param name="right"> The second metric entry to compare. </param>
	/// <returns> true if the metric entries are equal; otherwise, false. </returns>
	public static bool operator ==(MetricEntry left, MetricEntry right) => left.Equals(right);

	/// <summary>
	/// Determines whether two metric entries are not equal.
	/// </summary>
	/// <param name="left"> The first metric entry to compare. </param>
	/// <param name="right"> The second metric entry to compare. </param>
	/// <returns> true if the metric entries are not equal; otherwise, false. </returns>
	public static bool operator !=(MetricEntry left, MetricEntry right) => !left.Equals(right);
}
