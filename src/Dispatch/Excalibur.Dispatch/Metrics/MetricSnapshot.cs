// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Represents a snapshot of metric data at a point in time.
/// </summary>
public readonly struct MetricSnapshot(
	int metricId,
	MetricType type,
	long timestampTicks,
	double value,
	int labelSetId,
	long count = 1,
	double sum = 0,
	double min = 0,
	double max = 0,
	HistogramBucket[]? buckets = null) : IEquatable<MetricSnapshot>
{
	/// <summary>
	/// Gets the metric identifier.
	/// </summary>
	/// <value>The current <see cref="MetricId"/> value.</value>
	public int MetricId { get; } = metricId;

	/// <summary>
	/// Gets the metric type.
	/// </summary>
	/// <value>The current <see cref="Type"/> value.</value>
	public MetricType Type { get; } = type;

	/// <summary>
	/// Gets the timestamp in ticks.
	/// </summary>
	/// <value>The current <see cref="TimestampTicks"/> value.</value>
	public long TimestampTicks { get; } = timestampTicks;

	/// <summary>
	/// Gets the metric value.
	/// </summary>
	/// <value>The current <see cref="Value"/> value.</value>
	public double Value { get; } = value;

	/// <summary>
	/// Gets the label set identifier.
	/// </summary>
	/// <value>The current <see cref="LabelSetId"/> value.</value>
	public int LabelSetId { get; } = labelSetId;

	/// <summary>
	/// Gets the count of observations.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public long Count { get; } = count;

	/// <summary>
	/// Gets the sum of all observed values.
	/// </summary>
	/// <value>The current <see cref="Sum"/> value.</value>
	public double Sum { get; } = sum != 0 ? sum : value;

	/// <summary>
	/// Gets the minimum observed value.
	/// </summary>
	/// <value>The current <see cref="Min"/> value.</value>
	public double Min { get; } = min != 0 ? min : value;

	/// <summary>
	/// Gets the maximum observed value.
	/// </summary>
	/// <value>The current <see cref="Max"/> value.</value>
	public double Max { get; } = max != 0 ? max : value;

	/// <summary>
	/// Gets the histogram buckets if applicable.
	/// </summary>
	/// <value>The current <see cref="Buckets"/> value.</value>
	public HistogramBucket[]? Buckets { get; } = buckets;

	/// <summary>
	/// Determines whether two metric snapshots are equal.
	/// </summary>
	/// <param name="left"> The first metric snapshot to compare. </param>
	/// <param name="right"> The second metric snapshot to compare. </param>
	/// <returns> true if the metric snapshots are equal; otherwise, false. </returns>
	public static bool operator ==(MetricSnapshot left, MetricSnapshot right) => left.Equals(right);

	/// <summary>
	/// Determines whether two metric snapshots are not equal.
	/// </summary>
	/// <param name="left"> The first metric snapshot to compare. </param>
	/// <param name="right"> The second metric snapshot to compare. </param>
	/// <returns> true if the metric snapshots are not equal; otherwise, false. </returns>
	public static bool operator !=(MetricSnapshot left, MetricSnapshot right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified metric snapshot is equal to the current metric snapshot.
	/// </summary>
	/// <param name="other"> The metric snapshot to compare with the current metric snapshot. </param>
	/// <returns> true if the specified metric snapshot is equal to the current metric snapshot; otherwise, false. </returns>
	public bool Equals(MetricSnapshot other) =>
		MetricId == other.MetricId &&
		Type == other.Type &&
		TimestampTicks == other.TimestampTicks &&
		Value.Equals(other.Value) &&
		LabelSetId == other.LabelSetId &&
		Count == other.Count &&
		Sum.Equals(other.Sum) &&
		Min.Equals(other.Min) &&
		Max.Equals(other.Max) &&
		EqualityComparer<HistogramBucket[]?>.Default.Equals(Buckets, other.Buckets);

	/// <summary>
	/// Determines whether the specified object is equal to the current metric snapshot.
	/// </summary>
	/// <param name="obj"> The object to compare with the current metric snapshot. </param>
	/// <returns> true if the specified object is equal to the current metric snapshot; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is MetricSnapshot other && Equals(other);

	/// <summary>
	/// Returns the hash code for this metric snapshot.
	/// </summary>
	/// <returns> A hash code for the current metric snapshot. </returns>
	public override int GetHashCode()
	{
		var hash1 = HashCode.Combine(MetricId, Type, TimestampTicks, Value, LabelSetId);
		var hash2 = HashCode.Combine(Count, Sum, Min, Max, Buckets?.GetHashCode() ?? 0);
		return HashCode.Combine(hash1, hash2);
	}
}
