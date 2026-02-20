// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about worker utilization.
/// </summary>
public sealed class WorkerStatistics
{
	/// <summary>
	/// Gets the total number of workers.
	/// </summary>
	/// <value>
	/// The total number of workers.
	/// </value>
	public int TotalWorkers { get; init; }

	/// <summary>
	/// Gets the number of active workers.
	/// </summary>
	/// <value>
	/// The number of active workers.
	/// </value>
	public int ActiveWorkers { get; init; }

	/// <summary>
	/// Gets the number of pending work items.
	/// </summary>
	/// <value>
	/// The number of pending work items.
	/// </value>
	public int PendingWork { get; init; }

	/// <summary>
	/// Gets the total processed count.
	/// </summary>
	/// <value>
	/// The total processed count.
	/// </value>
	public long ProcessedCount { get; init; }

	/// <summary>
	/// Gets the total error count.
	/// </summary>
	/// <value>
	/// The total error count.
	/// </value>
	public long ErrorCount { get; init; }

	/// <summary>
	/// Gets the average processing time in milliseconds.
	/// </summary>
	/// <value>
	/// The average processing time in milliseconds.
	/// </value>
	public double AverageProcessingTime { get; init; }
}
