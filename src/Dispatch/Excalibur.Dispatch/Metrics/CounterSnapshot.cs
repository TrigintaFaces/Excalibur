// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Statistics for a counter metric.
/// </summary>
public struct CounterSnapshot : IEquatable<CounterSnapshot>
{
	/// <summary>
	/// Gets or sets the name of the counter metric. Used to identify the counter in metric collections and reporting systems.
	/// </summary>
	/// <value> The metric name identifier. </value>
	public string Name { get; set; }

	/// <summary>
	/// Gets or sets the current value of the counter. Represents the accumulated count at the time this snapshot was taken.
	/// </summary>
	/// <value> The counter value as a long integer. </value>
	public long Value { get; set; }

	/// <summary>
	/// Gets or sets the unit of measurement for the counter value. May be null if the counter represents a dimensionless quantity.
	/// </summary>
	/// <value> The unit string (e.g., "requests", "bytes") or null if dimensionless. </value>
	public string? Unit { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this snapshot was captured. Provides temporal context for the counter value in metric analysis.
	/// </summary>
	/// <value> The UTC timestamp of snapshot creation. </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Determines whether two snapshots are equal.
	/// </summary>
	/// <param name="left"> The first snapshot to compare. </param>
	/// <param name="right"> The second snapshot to compare. </param>
	/// <returns> true if the snapshots are equal; otherwise, false. </returns>
	public static bool operator ==(CounterSnapshot left, CounterSnapshot right) => left.Equals(right);

	/// <summary>
	/// Determines whether two snapshots are not equal.
	/// </summary>
	/// <param name="left"> The first snapshot to compare. </param>
	/// <param name="right"> The second snapshot to compare. </param>
	/// <returns> true if the snapshots are not equal; otherwise, false. </returns>
	public static bool operator !=(CounterSnapshot left, CounterSnapshot right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified snapshot is equal to the current snapshot.
	/// </summary>
	/// <param name="other"> The snapshot to compare with the current snapshot. </param>
	/// <returns> true if the specified snapshot is equal to the current snapshot; otherwise, false. </returns>
	public readonly bool Equals(CounterSnapshot other) => string.Equals(Name, other.Name, StringComparison.Ordinal) && Value == other.Value && string.Equals(Unit, other.Unit, StringComparison.Ordinal) && Timestamp.Equals(other.Timestamp);

	/// <summary>
	/// Determines whether the specified object is equal to the current snapshot.
	/// </summary>
	/// <param name="obj"> The object to compare with the current snapshot. </param>
	/// <returns> true if the specified object is equal to the current snapshot; otherwise, false. </returns>
	public override readonly bool Equals(object? obj) => obj is CounterSnapshot other && Equals(other);

	/// <summary>
	/// Returns the hash code for this snapshot.
	/// </summary>
	/// <returns> A hash code for the current snapshot. </returns>
	public override readonly int GetHashCode() => HashCode.Combine(Name, Value, Unit, Timestamp);
}
