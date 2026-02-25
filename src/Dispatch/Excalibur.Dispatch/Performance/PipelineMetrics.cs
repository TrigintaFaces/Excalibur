// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Pipeline execution performance metrics.
/// </summary>
public sealed record PipelineMetrics
{
	/// <summary>
	/// Gets the total number of pipeline executions.
	/// </summary>
	/// <value> The count of pipeline executions sampled. </value>
	public required int TotalExecutions { get; init; }

	/// <summary>
	/// Gets the total duration of all pipeline executions.
	/// </summary>
	/// <value> The cumulative duration across all executions. </value>
	public required TimeSpan TotalDuration { get; init; }

	/// <summary>
	/// Gets the average duration per pipeline execution.
	/// </summary>
	/// <value> The mean duration per execution. </value>
	public required TimeSpan AverageDuration { get; init; }

	/// <summary>
	/// Gets the average number of middleware components per pipeline.
	/// </summary>
	/// <value> The average middleware count per execution. </value>
	public required double AverageMiddlewareCount { get; init; }

	/// <summary>
	/// Gets the total memory allocated during all pipeline executions.
	/// </summary>
	/// <value> The total allocated bytes across executions. </value>
	public required long TotalMemoryAllocated { get; init; }

	/// <summary>
	/// Gets the average memory allocated per pipeline execution.
	/// </summary>
	/// <value> The mean allocated bytes per execution. </value>
	public required long AverageMemoryPerExecution { get; init; }
}
