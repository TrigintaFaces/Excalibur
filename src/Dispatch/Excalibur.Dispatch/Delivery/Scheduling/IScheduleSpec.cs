// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines the scheduling specification for a message including cron expression,
/// timezone, interval, next execution time, and enabled state.
/// </summary>
public interface IScheduleSpec
{
	/// <summary>
	/// Gets or sets the cron expression that defines the recurring execution schedule.
	/// </summary>
	/// <value>
	/// A valid cron expression string defining when the message should be executed.
	/// </value>
	string CronExpression { get; set; }

	/// <summary>
	/// Gets or sets the timezone identifier for evaluating cron expressions in local time.
	/// </summary>
	/// <value>
	/// A standard timezone identifier (e.g., "America/New_York", "Europe/London"). Null indicates UTC.
	/// </value>
	string? TimeZoneId { get; set; }

	/// <summary>
	/// Gets or sets the fixed interval for simple recurring schedule patterns.
	/// </summary>
	/// <value>
	/// A TimeSpan representing the interval between executions. Null indicates this is not an interval-based schedule.
	/// </value>
	TimeSpan? Interval { get; set; }

	/// <summary>
	/// Gets or sets the next scheduled execution time in UTC for efficient querying and execution planning.
	/// </summary>
	/// <value> The UTC timestamp when this message should next be executed, or null if the schedule is complete. </value>
	DateTimeOffset? NextExecutionUtc { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this scheduled message is active and should be executed.
	/// </summary>
	/// <value> True if the schedule is active; false to pause execution without deleting the schedule. </value>
	bool Enabled { get; set; }
}
