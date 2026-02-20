// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Represents statistical information about message pool usage and performance. This structure provides metrics for tracking pool
/// efficiency, allocation patterns, and current utilization.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct PoolStatistics : IEquatable<PoolStatistics>
{
	/// <summary>
	/// Gets the total number of items that have been rented from the pool since its creation.
	/// </summary>
	/// <value>The current <see cref="TotalRented"/> value.</value>
	public int TotalRented { get; init; }

	/// <summary>
	/// Gets the total number of items that have been returned to the pool since its creation.
	/// </summary>
	/// <value>The current <see cref="TotalReturned"/> value.</value>
	public int TotalReturned { get; init; }

	/// <summary>
	/// Gets the current number of items that are currently rented from the pool and not yet returned.
	/// </summary>
	/// <value>The current <see cref="CurrentlyRented"/> value.</value>
	public int CurrentlyRented { get; init; }

	/// <summary>
	/// Gets the current size of the pool, representing the total capacity of items available for rental.
	/// </summary>
	/// <value>The current <see cref="PoolSize"/> value.</value>
	public int PoolSize { get; init; }

	/// <summary>
	/// Gets the total number of allocations that have occurred, including both pooled and non-pooled allocations.
	/// </summary>
	/// <value>The current <see cref="TotalAllocations"/> value.</value>
	public long TotalAllocations { get; init; }

	/// <summary>
	/// Determines whether two PoolStatistics instances are equal.
	/// </summary>
	/// <param name="left"> The first PoolStatistics instance to compare. </param>
	/// <param name="right"> The second PoolStatistics instance to compare. </param>
	/// <returns> true if the instances are equal; otherwise, false. </returns>
	public static bool operator ==(PoolStatistics left, PoolStatistics right) => left.Equals(right);

	/// <summary>
	/// Determines whether two PoolStatistics instances are not equal.
	/// </summary>
	/// <param name="left"> The first PoolStatistics instance to compare. </param>
	/// <param name="right"> The second PoolStatistics instance to compare. </param>
	/// <returns> true if the instances are not equal; otherwise, false. </returns>
	public static bool operator !=(PoolStatistics left, PoolStatistics right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified object is equal to the current PoolStatistics instance.
	/// </summary>
	/// <param name="obj"> The object to compare with the current instance. </param>
	/// <returns> true if the specified object is equal to the current instance; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is PoolStatistics other && Equals(other);

	/// <summary>
	/// Indicates whether the current PoolStatistics instance is equal to another PoolStatistics instance.
	/// </summary>
	/// <param name="other"> The PoolStatistics instance to compare with this instance. </param>
	/// <returns> true if the current instance is equal to the other parameter; otherwise, false. </returns>
	public bool Equals(PoolStatistics other) =>
		TotalRented == other.TotalRented &&
		TotalReturned == other.TotalReturned &&
		CurrentlyRented == other.CurrentlyRented &&
		PoolSize == other.PoolSize &&
		TotalAllocations == other.TotalAllocations;

	/// <summary>
	/// Returns the hash code for this PoolStatistics instance.
	/// </summary>
	/// <returns> A 32-bit signed integer that is the hash code for this instance. </returns>
	public override int GetHashCode() => HashCode.Combine(TotalRented, TotalReturned, CurrentlyRented, PoolSize, TotalAllocations);
}
