// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Performance metrics for a component.
/// </summary>
public sealed record ComponentMetrics
{
	/// <summary>
	/// Gets the total number of executions for the component.
	/// </summary>
	/// <value> The number of executions observed during the sampling window. </value>
	public required int ExecutionCount { get; init; }

	/// <summary>
	/// Gets the total duration of all executions.
	/// </summary>
	/// <value> The aggregate execution time. </value>
	public required TimeSpan TotalDuration { get; init; }

	/// <summary>
	/// Gets the average duration per execution.
	/// </summary>
	/// <value> The mean execution time per invocation. </value>
	public required TimeSpan AverageDuration { get; init; }

	/// <summary>
	/// Gets the minimum execution duration.
	/// </summary>
	/// <value> The shortest execution time recorded. </value>
	public required TimeSpan MinDuration { get; init; }

	/// <summary>
	/// Gets the maximum execution duration.
	/// </summary>
	/// <value> The longest execution time recorded. </value>
	public required TimeSpan MaxDuration { get; init; }

	/// <summary>
	/// Gets the number of successful executions.
	/// </summary>
	/// <value> The count of successful runs. </value>
	public required int SuccessCount { get; init; }

	/// <summary>
	/// Gets the number of failed executions.
	/// </summary>
	/// <value> The count of failed runs. </value>
	public required int FailureCount { get; init; }

	/// <summary>
	/// Gets the success rate as a percentage (0.0 to 1.0).
	/// </summary>
	/// <value> The ratio of successful executions to total executions. </value>
	public required double SuccessRate { get; init; }
}
