// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Statistics for the dedicated thread processor.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct ProcessorStatistics(
	long totalMessagesProcessed,
	long totalErrors,
	double averageLatencyUs,
	double p99LatencyUs,
	int activeThreads) : IEquatable<ProcessorStatistics>
{
	/// <summary>
	/// Gets the total number of messages processed.
	/// </summary>
	/// <value>The current <see cref="TotalMessagesProcessed"/> value.</value>
	public long TotalMessagesProcessed { get; } = totalMessagesProcessed;

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
	/// Gets the 99th percentile latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="P99LatencyUs"/> value.</value>
	public double P99LatencyUs { get; } = p99LatencyUs;

	/// <summary>
	/// Gets the number of active threads.
	/// </summary>
	/// <value>The current <see cref="ActiveThreads"/> value.</value>
	public int ActiveThreads { get; } = activeThreads;

	/// <summary>
	/// Determines whether two statistics are equal.
	/// </summary>
	/// <param name="left"> The first statistics to compare. </param>
	/// <param name="right"> The second statistics to compare. </param>
	/// <returns> true if the statistics are equal; otherwise, false. </returns>
	public static bool operator ==(ProcessorStatistics left, ProcessorStatistics right) => left.Equals(right);

	/// <summary>
	/// Determines whether two statistics are not equal.
	/// </summary>
	/// <param name="left"> The first statistics to compare. </param>
	/// <param name="right"> The second statistics to compare. </param>
	/// <returns> true if the statistics are not equal; otherwise, false. </returns>
	public static bool operator !=(ProcessorStatistics left, ProcessorStatistics right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified statistics is equal to the current statistics.
	/// </summary>
	/// <param name="other"> The statistics to compare with the current statistics. </param>
	/// <returns> true if the specified statistics is equal to the current statistics; otherwise, false. </returns>
	public bool Equals(ProcessorStatistics other) =>
		TotalMessagesProcessed == other.TotalMessagesProcessed &&
		TotalErrors == other.TotalErrors &&
		AverageLatencyUs.Equals(other.AverageLatencyUs) &&
		P99LatencyUs.Equals(other.P99LatencyUs) &&
		ActiveThreads == other.ActiveThreads;

	/// <summary>
	/// Determines whether the specified object is equal to the current statistics.
	/// </summary>
	/// <param name="obj"> The object to compare with the current statistics. </param>
	/// <returns> true if the specified object is equal to the current statistics; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is ProcessorStatistics other && Equals(other);

	/// <summary>
	/// Returns the hash code for this statistics.
	/// </summary>
	/// <returns> A hash code for the current statistics. </returns>
	public override int GetHashCode() =>
		HashCode.Combine(TotalMessagesProcessed, TotalErrors, AverageLatencyUs, P99LatencyUs, ActiveThreads);
}
