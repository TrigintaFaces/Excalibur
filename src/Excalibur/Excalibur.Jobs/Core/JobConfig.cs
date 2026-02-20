// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Base class for job configurations.
/// </summary>
public abstract class JobConfig : IJobConfig
{
	/// <summary>
	/// Gets the job name.
	/// </summary>
	/// <value>
	/// The job name.
	/// </value>
	public string JobName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the job group.
	/// </summary>
	/// <value>
	/// The job group.
	/// </value>
	public string JobGroup { get; init; } = "Default";

	/// <summary>
	/// Gets the cron expression that defines the job's schedule.
	/// </summary>
	/// <value>
	/// The cron expression that defines the job's schedule.
	/// </value>
	public string CronSchedule { get; init; } = string.Empty;

	/// <summary>
	/// Gets the threshold duration after which the job's state is considered degraded.
	/// </summary>
	/// <value>
	/// The threshold duration after which the job's state is considered degraded.
	/// </value>
	public TimeSpan DegradedThreshold { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets a value indicating whether the job is disabled.
	/// </summary>
	/// <value>
	/// A value indicating whether the job is disabled.
	/// </value>
	public bool Disabled { get; init; }

	/// <summary>
	/// Gets the threshold duration after which the job's state is considered unhealthy.
	/// </summary>
	/// <value>
	/// The threshold duration after which the job's state is considered unhealthy.
	/// </value>
	public TimeSpan UnhealthyThreshold { get; init; } = TimeSpan.FromMinutes(10);
}
