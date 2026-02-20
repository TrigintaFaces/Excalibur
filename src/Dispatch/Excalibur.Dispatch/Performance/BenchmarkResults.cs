// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Results from a comprehensive performance benchmark run.
/// </summary>
public sealed record BenchmarkResults
{
	/// <summary>
	/// Gets the date and time when the benchmark was run.
	/// </summary>
	/// <value> The timestamp recorded for the benchmark execution. </value>
	public required DateTimeOffset TestDate { get; init; }

	/// <summary>
	/// Gets the number of iterations executed in the benchmark.
	/// </summary>
	/// <value> The total iterations completed. </value>
	public required int Iterations { get; init; }

	/// <summary>
	/// Gets or sets the total duration of the benchmark run.
	/// </summary>
	/// <value> The aggregate elapsed time for the benchmark run. </value>
	public TimeSpan TotalDuration { get; set; }

	/// <summary>
	/// Gets or sets the calculated messages processed per second.
	/// </summary>
	/// <value> The throughput measured in messages per second. </value>
	public double MessagesPerSecond { get; set; }

	/// <summary>
	/// Gets or sets the average latency per message in milliseconds.
	/// </summary>
	/// <value> The per-message latency in milliseconds. </value>
	public double AverageLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the performance snapshot captured during the benchmark.
	/// </summary>
	/// <value> The detailed performance snapshot associated with the benchmark. </value>
	public PerformanceSnapshot? PerformanceSnapshot { get; set; }
}
