// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Configuration options for cron-based scheduling.
/// </summary>
public sealed class CronScheduleOptions
{
	/// <summary>
	/// Gets or sets the default timezone to use when none is specified. Defaults to UTC.
	/// </summary>
	/// <value>The current <see cref="DefaultTimeZone"/> value.</value>
	public TimeZoneInfo DefaultTimeZone { get; set; } = TimeZoneInfo.Utc;

	/// <summary>
	/// Gets or sets a value indicating whether to include seconds in cron expressions by default. When true, expects 6-field format
	/// (seconds minutes hours day month weekday). When false, expects 5-field format (minutes hours day month weekday). Defaults to false.
	/// </summary>
	/// <value>The current <see cref="IncludeSeconds"/> value.</value>
	public bool IncludeSeconds { get; set; }

	/// <summary>
	/// Gets or sets how to handle missed executions (e.g., due to system downtime).
	/// </summary>
	/// <value>The current <see cref="MissedExecutionBehavior"/> value.</value>
	public MissedExecutionBehavior MissedExecutionBehavior { get; set; } = MissedExecutionBehavior.SkipMissed;

	/// <summary>
	/// Gets or sets the maximum number of missed executions to catch up on. Only applies when MissedExecutionBehavior is ExecuteAllMissed.
	/// </summary>
	/// <value>The current <see cref="MaxMissedExecutions"/> value.</value>
	public int MaxMissedExecutions { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically adjust for daylight saving time transitions. When true, schedules will
	/// automatically adjust when DST changes occur.
	/// </summary>
	/// <value>The current <see cref="AutoAdjustForDaylightSaving"/> value.</value>
	public bool AutoAdjustForDaylightSaving { get; set; } = true;

	/// <summary>
	/// Gets or sets the tolerance window for execution timing. Jobs scheduled within this window of their target time are considered on-time.
	/// </summary>
	/// <value>
	/// The tolerance window for execution timing. Jobs scheduled within this window of their target time are considered on-time.
	/// </value>
	public TimeSpan ExecutionToleranceWindow { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets a value indicating whether to enable extended cron syntax features. This includes special characters like L (last), W
	/// (weekday), # (nth occurrence).
	/// </summary>
	/// <value>The current <see cref="EnableExtendedSyntax"/> value.</value>
	public bool EnableExtendedSyntax { get; set; } = true;

	/// <summary>
	/// Gets the supported timezones for scheduling. If empty, all system timezones are supported.
	/// </summary>
	/// <value>The current <see cref="SupportedTimeZoneIds"/> value.</value>
	public HashSet<string> SupportedTimeZoneIds { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to log detailed cron evaluation information.
	/// </summary>
	/// <value>The current <see cref="EnableDetailedLogging"/> value.</value>
	public bool EnableDetailedLogging { get; set; }
}
