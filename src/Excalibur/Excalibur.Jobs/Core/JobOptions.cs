// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.Core;

/// <summary>
/// Base class for job configurations.
/// </summary>
public abstract class JobOptions : IJobOptions
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
	/// <see langword="true"/> if the job is disabled; otherwise, <see langword="false"/>.
	/// The default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// A built-in job's <c>ConfigureJob</c> method honors this flag at scheduling time: when
	/// <see langword="true"/>, the job and its trigger are never registered with the scheduler, so
	/// no trigger fires.
	/// </para>
	/// <para>
	/// This schedule-time gate is sufficient for the default in-memory job store. With a persistent
	/// Quartz job store a job that was previously scheduled survives across restarts; skipping
	/// registration does not delete it, so a job persisted while enabled keeps firing after this flag
	/// is later set to <see langword="true"/>. To disable an already-persisted job, register the
	/// runtime watcher via <c>AddJobWatcher&lt;TJob, TOptions&gt;(...)</c> — it pauses and resumes the
	/// job through the scheduler and the paused state is itself persisted — or delete the job from the
	/// store.
	/// </para>
	/// </remarks>
	public bool Disabled { get; init; }

	/// <summary>
	/// Gets the threshold duration after which the job's state is considered unhealthy.
	/// </summary>
	/// <value>
	/// The threshold duration after which the job's state is considered unhealthy.
	/// </value>
	public TimeSpan UnhealthyThreshold { get; init; } = TimeSpan.FromMinutes(10);
}
