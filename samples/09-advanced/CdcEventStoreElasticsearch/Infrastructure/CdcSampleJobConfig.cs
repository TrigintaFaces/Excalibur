// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace CdcEventStoreElasticsearch.Infrastructure;

/// <summary>
/// Configuration options for the CDC Quartz job.
/// </summary>
public sealed class CdcSampleJobConfig
{
	/// <summary>
	/// Configuration section name.
	/// </summary>
	public const string SectionName = "Jobs:CdcSampleJob";

	/// <summary>Gets or sets whether the job is enabled.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Gets or sets the job name.</summary>
	public string JobName { get; set; } = "CdcSampleJob";

	/// <summary>Gets or sets the job group.</summary>
	public string JobGroup { get; set; } = "CDC";

	/// <summary>Gets or sets the cron schedule for the job.</summary>
	/// <remarks>Default runs every 5 seconds.</remarks>
	public string CronSchedule { get; set; } = "0/5 * * * * ?";

	/// <summary>Gets or sets the CDC capture instances to process.</summary>
	public string[] CaptureInstances { get; set; } = ["dbo_LegacyCustomers", "dbo_LegacyOrders", "dbo_LegacyOrderItems"];

	/// <summary>Gets or sets the CDC source connection string.</summary>
	public string? CdcSourceConnectionString { get; set; }

	/// <summary>Gets or sets the CDC state store connection string.</summary>
	public string? StateStoreConnectionString { get; set; }

	/// <summary>Gets or sets the maximum batch size per poll.</summary>
	public int BatchSize { get; set; } = 100;
}
