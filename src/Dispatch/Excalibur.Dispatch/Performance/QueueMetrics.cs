// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Queue operation performance metrics.
/// </summary>
public sealed record QueueMetrics
{
	/// <summary>
	/// Gets the performance metrics for different queue operations.
	/// </summary>
	/// <value> The per-operation metrics captured for the queue. </value>
	public required IReadOnlyDictionary<string, ComponentMetrics> OperationMetrics { get; init; }

	/// <summary>
	/// Gets the current queue depth.
	/// </summary>
	/// <value> The queue depth at the time of measurement. </value>
	public required int CurrentDepth { get; init; }

	/// <summary>
	/// Gets the maximum queue depth reached.
	/// </summary>
	/// <value> The highest recorded queue depth. </value>
	public required int MaxDepthReached { get; init; }

	/// <summary>
	/// Gets the average queue depth over time.
	/// </summary>
	/// <value> The average queue depth calculated across samples. </value>
	public required double AverageDepth { get; init; }

	/// <summary>
	/// Gets the throughput in operations per second.
	/// </summary>
	/// <value> The number of operations processed per second. </value>
	public required double ThroughputOperationsPerSecond { get; init; }
}
