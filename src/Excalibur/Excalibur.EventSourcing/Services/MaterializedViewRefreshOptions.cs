// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Services;

/// <summary>
/// Configuration options for the <see cref="MaterializedViewRefreshService"/>.
/// </summary>
public sealed class MaterializedViewRefreshOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to catch up all views on application startup.
	/// </summary>
	/// <value><see langword="true"/> to enable catch-up on startup; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool CatchUpOnStartup { get; set; }

	/// <summary>
	/// Gets or sets the refresh interval for polling-based scheduling.
	/// </summary>
	/// <value>The interval between refresh cycles. Default is 30 seconds. Set to <see langword="null"/> to disable interval-based scheduling.</value>
	/// <remarks>
	/// <para>
	/// If both <see cref="RefreshInterval"/> and <see cref="CronExpression"/> are set,
	/// the cron expression takes precedence.
	/// </para>
	/// </remarks>
	public TimeSpan? RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the cron expression for schedule-based refreshes.
	/// </summary>
	/// <value>A cron expression (5-part format: minute, hour, day-of-month, month, day-of-week). Default is <see langword="null"/>.</value>
	/// <remarks>
	/// <para>
	/// When set, cron-based scheduling takes precedence over <see cref="RefreshInterval"/>.
	/// Uses NCrontab format: "* * * * *" (minute hour day-of-month month day-of-week).
	/// </para>
	/// <para>
	/// <b>Examples:</b>
	/// <list type="bullet">
	/// <item><c>"*/5 * * * *"</c> - Every 5 minutes</item>
	/// <item><c>"0 * * * *"</c> - Every hour at minute 0</item>
	/// <item><c>"0 0 * * *"</c> - Daily at midnight</item>
	/// <item><c>"0 2 * * 0"</c> - Weekly on Sunday at 2 AM</item>
	/// </list>
	/// </para>
	/// </remarks>
	public string? CronExpression { get; set; }

	/// <summary>
	/// Gets or sets the batch size for event processing during refresh.
	/// </summary>
	/// <value>The number of events to process per batch. Default is 100.</value>
	[Range(1, 10000)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the initial delay for exponential backoff retry on transient failures.
	/// </summary>
	/// <value>The initial retry delay. Default is 1 second.</value>
	public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay for exponential backoff retry.
	/// </summary>
	/// <value>The maximum retry delay. Default is 5 minutes.</value>
	public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of retry attempts before giving up.
	/// </summary>
	/// <value>The maximum retry count. Default is 5. Set to 0 for infinite retries.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryCount { get; set; } = 5;

	/// <summary>
	/// Gets or sets a value indicating whether the service is enabled.
	/// </summary>
	/// <value><see langword="true"/> to enable the refresh service; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool Enabled { get; set; } = true;
}
