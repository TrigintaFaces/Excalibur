// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using NCrontab;

namespace Excalibur.EventSourcing.Services;

/// <summary>
/// Wraps NCrontab for cron-based scheduling with testable time provider support.
/// </summary>
internal sealed class CronSchedule
{
	private readonly CrontabSchedule _schedule;

	/// <summary>
	/// Initializes a new instance of the <see cref="CronSchedule"/> class.
	/// </summary>
	/// <param name="cronExpression">The cron expression in 5-part format.</param>
	/// <exception cref="ArgumentException">Thrown when the cron expression is invalid.</exception>
	public CronSchedule(string cronExpression)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		try
		{
			_schedule = CrontabSchedule.Parse(cronExpression);
		}
		catch (CrontabException ex)
		{
			throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression), ex);
		}
	}

	/// <summary>
	/// Gets the next occurrence after the specified time.
	/// </summary>
	/// <param name="fromTime">The time to calculate the next occurrence from.</param>
	/// <returns>The next scheduled occurrence.</returns>
	public DateTimeOffset GetNextOccurrence(DateTimeOffset fromTime)
	{
		var nextUtc = _schedule.GetNextOccurrence(fromTime.UtcDateTime);
		return new DateTimeOffset(nextUtc, TimeSpan.Zero);
	}

	/// <summary>
	/// Calculates the delay until the next occurrence.
	/// </summary>
	/// <param name="fromTime">The current time.</param>
	/// <returns>The time span until the next occurrence.</returns>
	public TimeSpan GetDelayUntilNext(DateTimeOffset fromTime)
	{
		var next = GetNextOccurrence(fromTime);
		var delay = next - fromTime;
		return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
	}

	/// <summary>
	/// Tries to parse a cron expression.
	/// </summary>
	/// <param name="cronExpression">The cron expression to parse.</param>
	/// <param name="schedule">The parsed schedule, or null if parsing fails.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryParse(string? cronExpression, out CronSchedule? schedule)
	{
		schedule = null;

		if (string.IsNullOrWhiteSpace(cronExpression))
		{
			return false;
		}

		try
		{
			schedule = new CronSchedule(cronExpression);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
