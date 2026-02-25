// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides timeout statistics for adaptive timeout calculations. R7.4: Statistical tracking for timeout optimization.
/// </summary>
public sealed class TimeoutStatistics
{
	/// <summary>
	/// Gets the operation type these statistics apply to.
	/// </summary>
	/// <value> The operation classification that produced the data. </value>
	public TimeoutOperationType OperationType { get; init; }

	/// <summary>
	/// Gets the total number of operations recorded.
	/// </summary>
	/// <value> The total sample size. </value>
	public int TotalOperations { get; init; }

	/// <summary>
	/// Gets the number of operations that completed successfully.
	/// </summary>
	/// <value> The count of successful operations. </value>
	public int SuccessfulOperations { get; init; }

	/// <summary>
	/// Gets the number of operations that timed out.
	/// </summary>
	/// <value> The count of timed-out operations. </value>
	public int TimedOutOperations { get; init; }

	/// <summary>
	/// Gets the average duration of successful operations.
	/// </summary>
	/// <value> The arithmetic mean duration. </value>
	public TimeSpan AverageDuration { get; init; }

	/// <summary>
	/// Gets the median duration of successful operations.
	/// </summary>
	/// <value> The median duration value. </value>
	public TimeSpan MedianDuration { get; init; }

	/// <summary>
	/// Gets the 95th percentile duration of successful operations.
	/// </summary>
	/// <value> The duration at the 95th percentile. </value>
	public TimeSpan P95Duration { get; init; }

	/// <summary>
	/// Gets the 99th percentile duration of successful operations.
	/// </summary>
	/// <value> The duration at the 99th percentile. </value>
	public TimeSpan P99Duration { get; init; }

	/// <summary>
	/// Gets the minimum duration recorded.
	/// </summary>
	/// <value> The shortest observed duration. </value>
	public TimeSpan MinDuration { get; init; }

	/// <summary>
	/// Gets the maximum duration recorded.
	/// </summary>
	/// <value> The longest observed duration. </value>
	public TimeSpan MaxDuration { get; init; }

	/// <summary>
	/// Gets the success rate as a percentage (0-100).
	/// </summary>
	/// <value> The percentage of operations that succeeded. </value>
	public double SuccessRate => TotalOperations > 0 ? SuccessfulOperations / (double)TotalOperations * 100.0 : 0.0;

	/// <summary>
	/// Gets the timeout rate as a percentage (0-100).
	/// </summary>
	/// <value> The percentage of operations that timed out. </value>
	public double TimeoutRate => TotalOperations > 0 ? TimedOutOperations / (double)TotalOperations * 100.0 : 0.0;

	/// <summary>
	/// Gets the timestamp when these statistics were last updated.
	/// </summary>
	/// <value> The timestamp of the most recent update. </value>
	public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the duration for the specified percentile.
	/// </summary>
	/// <param name="percentile"> The percentile (50-99). </param>
	/// <returns> The duration at the specified percentile. </returns>
	public TimeSpan GetPercentileDuration(int percentile) => percentile switch
	{
		<= 50 => MedianDuration,
		<= 95 => P95Duration,
		<= 99 => P99Duration,
		_ => MaxDuration,
	};

	/// <summary>
	/// Determines if the statistics contain sufficient data for reliable calculations.
	/// </summary>
	/// <param name="minimumSamples"> The minimum number of samples required. </param>
	/// <returns> <see langword="true" /> if sufficient data is available; otherwise, <see langword="false" />. </returns>
	public bool HasSufficientData(int minimumSamples = 100) => TotalOperations >= minimumSamples && SuccessfulOperations > 0;
}
