// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Represents a histogram bucket with count.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct HistogramBucket(double upperBound, long count) : IEquatable<HistogramBucket>
{
	/// <summary>
	/// Gets the upper bound of the bucket.
	/// </summary>
	/// <value>The current <see cref="UpperBound"/> value.</value>
	public double UpperBound { get; } = upperBound;

	/// <summary>
	/// Gets the count of items in the bucket.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public long Count { get; } = count;

	/// <summary>
	/// Determines whether two buckets are equal.
	/// </summary>
	/// <param name="left"> The first bucket to compare. </param>
	/// <param name="right"> The second bucket to compare. </param>
	/// <returns> true if the buckets are equal; otherwise, false. </returns>
	public static bool operator ==(HistogramBucket left, HistogramBucket right) => left.Equals(right);

	/// <summary>
	/// Determines whether two buckets are not equal.
	/// </summary>
	/// <param name="left"> The first bucket to compare. </param>
	/// <param name="right"> The second bucket to compare. </param>
	/// <returns> true if the buckets are not equal; otherwise, false. </returns>
	public static bool operator !=(HistogramBucket left, HistogramBucket right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified bucket is equal to the current bucket.
	/// </summary>
	/// <param name="other"> The bucket to compare with the current bucket. </param>
	/// <returns> true if the specified bucket is equal to the current bucket; otherwise, false. </returns>
	public bool Equals(HistogramBucket other) => UpperBound.Equals(other.UpperBound) && Count == other.Count;

	/// <summary>
	/// Determines whether the specified object is equal to the current bucket.
	/// </summary>
	/// <param name="obj"> The object to compare with the current bucket. </param>
	/// <returns> true if the specified object is equal to the current bucket; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is HistogramBucket other && Equals(other);

	/// <summary>
	/// Returns the hash code for this bucket.
	/// </summary>
	/// <returns> A hash code for the current bucket. </returns>
	public override int GetHashCode() => HashCode.Combine(UpperBound, Count);
}
