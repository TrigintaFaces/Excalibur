// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Statistics for the synchronous dispatcher.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct DispatcherStatistics(
	long totalMessagesDispatched,
	long totalErrors,
	double averageLatencyUs,
	double p50LatencyUs,
	double p95LatencyUs,
	double p99LatencyUs,
	double lastLatencyUs) : IEquatable<DispatcherStatistics>
{
	/// <summary>
	/// Gets the total number of messages dispatched.
	/// </summary>
	/// <value>The current <see cref="TotalMessagesDispatched"/> value.</value>
	public long TotalMessagesDispatched { get; } = totalMessagesDispatched;

	/// <summary>
	/// Gets the total number of errors.
	/// </summary>
	/// <value>The current <see cref="TotalErrors"/> value.</value>
	public long TotalErrors { get; } = totalErrors;

	/// <summary>
	/// Gets the average latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="AverageLatencyUs"/> value.</value>
	public double AverageLatencyUs { get; } = averageLatencyUs;

	/// <summary>
	/// Gets the 50th percentile latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="P50LatencyUs"/> value.</value>
	public double P50LatencyUs { get; } = p50LatencyUs;

	/// <summary>
	/// Gets the 95th percentile latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="P95LatencyUs"/> value.</value>
	public double P95LatencyUs { get; } = p95LatencyUs;

	/// <summary>
	/// Gets the 99th percentile latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="P99LatencyUs"/> value.</value>
	public double P99LatencyUs { get; } = p99LatencyUs;

	/// <summary>
	/// Gets the last recorded latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="LastLatencyUs"/> value.</value>
	public double LastLatencyUs { get; } = lastLatencyUs;

	/// <summary>
	/// Determines whether two statistics are equal.
	/// </summary>
	/// <param name="left"> The first statistics to compare. </param>
	/// <param name="right"> The second statistics to compare. </param>
	/// <returns> true if the statistics are equal; otherwise, false. </returns>
	public static bool operator ==(DispatcherStatistics left, DispatcherStatistics right) => left.Equals(right);

	/// <summary>
	/// Determines whether two statistics are not equal.
	/// </summary>
	/// <param name="left"> The first statistics to compare. </param>
	/// <param name="right"> The second statistics to compare. </param>
	/// <returns> true if the statistics are not equal; otherwise, false. </returns>
	public static bool operator !=(DispatcherStatistics left, DispatcherStatistics right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified statistics is equal to the current statistics.
	/// </summary>
	/// <param name="other"> The statistics to compare with the current statistics. </param>
	/// <returns> true if the specified statistics is equal to the current statistics; otherwise, false. </returns>
	public bool Equals(DispatcherStatistics other) =>
		TotalMessagesDispatched == other.TotalMessagesDispatched &&
		TotalErrors == other.TotalErrors &&
		AverageLatencyUs.Equals(other.AverageLatencyUs) &&
		P50LatencyUs.Equals(other.P50LatencyUs) &&
		P95LatencyUs.Equals(other.P95LatencyUs) &&
		P99LatencyUs.Equals(other.P99LatencyUs) &&
		LastLatencyUs.Equals(other.LastLatencyUs);

	/// <summary>
	/// Determines whether the specified object is equal to the current statistics.
	/// </summary>
	/// <param name="obj"> The object to compare with the current statistics. </param>
	/// <returns> true if the specified object is equal to the current statistics; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is DispatcherStatistics other && Equals(other);

	/// <summary>
	/// Returns the hash code for this statistics.
	/// </summary>
	/// <returns> A hash code for the current statistics. </returns>
	public override int GetHashCode() =>
		HashCode.Combine(TotalMessagesDispatched, TotalErrors, AverageLatencyUs, P50LatencyUs, P95LatencyUs, P99LatencyUs,
			LastLatencyUs);
}
