// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Report of thread utilization.
/// </summary>
public sealed class UtilizationReport
{
	/// <summary>
	/// Gets the total number of threads.
	/// </summary>
	/// <value>
	/// The total number of threads.
	/// </value>
	public int TotalThreads { get; init; }

	/// <summary>
	/// Gets the number of currently active threads.
	/// </summary>
	/// <value>
	/// The number of currently active threads.
	/// </value>
	public int ActiveThreads { get; init; }

	/// <summary>
	/// Gets the maximum number of threads observed active.
	/// </summary>
	/// <value>
	/// The maximum number of threads observed active.
	/// </value>
	public int MaxObservedThreads { get; init; }

	/// <summary>
	/// Gets the average utilization percentage.
	/// </summary>
	/// <value>
	/// The average utilization percentage.
	/// </value>
	public double AverageUtilization { get; init; }

	/// <summary>
	/// Gets the average processing time per task.
	/// </summary>
	/// <value>
	/// The average processing time per task.
	/// </value>
	public TimeSpan AverageProcessingTime { get; init; }

	/// <summary>
	/// Gets the number of context switches.
	/// </summary>
	/// <value>
	/// The number of context switches.
	/// </value>
	public long ContextSwitchCount { get; init; }

	/// <summary>
	/// Gets the total number of tasks processed.
	/// </summary>
	/// <value>
	/// The total number of tasks processed.
	/// </value>
	public long TotalTasksProcessed { get; init; }
}
