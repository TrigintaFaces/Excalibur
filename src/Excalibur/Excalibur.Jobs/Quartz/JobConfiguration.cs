// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Configuration information for a job instance.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobConfiguration" /> class. </remarks>
/// <param name="jobKey"> The unique key for this job instance. </param>
/// <param name="cronExpression"> The cron expression for scheduling. </param>
public sealed class JobConfiguration(string jobKey, string cronExpression)
{
	/// <summary>
	/// Gets the unique key for this job instance.
	/// </summary>
	/// <value>
	/// The unique key for this job instance.
	/// </value>
	public string JobKey { get; } = jobKey ?? throw new ArgumentNullException(nameof(jobKey));

	/// <summary>
	/// Gets the cron expression for scheduling.
	/// </summary>
	/// <value>
	/// The cron expression for scheduling.
	/// </value>
	public string CronExpression { get; } = cronExpression ?? throw new ArgumentNullException(nameof(cronExpression));

	/// <summary>
	/// Gets or sets additional data to pass to the job.
	/// </summary>
	/// <value>
	/// Additional data to pass to the job.
	/// </value>
	public object? JobData { get; set; }

	/// <summary>
	/// Gets or sets a description for this job instance.
	/// </summary>
	/// <value>
	/// A description for this job instance.
	/// </value>
	public string? Description { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this job instance is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether this job instance is enabled.
	/// </value>
	public bool Enabled { get; set; } = true;
}
