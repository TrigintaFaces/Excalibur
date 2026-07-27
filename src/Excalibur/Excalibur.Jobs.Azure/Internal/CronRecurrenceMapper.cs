// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Cronos;

namespace Excalibur.Jobs.Azure.Internal;

/// <summary>
/// Maps a standard 5-field cron expression to the subset of shapes expressible as an Azure Logic Apps
/// recurrence trigger. Azure Logic Apps recurrence triggers do not accept raw cron syntax; they use a
/// <c>frequency</c> + <c>interval</c> pair with an optional <c>schedule</c> object (<c>hours</c>,
/// <c>minutes</c>, <c>weekDays</c>). Only a clean, common subset of cron can be represented this way.
/// </summary>
/// <remarks>
/// Any cron expression outside the supported subset throws <see cref="NotSupportedException"/> rather
/// than being approximated, so a schedule is never silently substituted for the one that was configured.
/// </remarks>
internal static partial class CronRecurrenceMapper
{
	private static readonly string[] DayNames =
	[
		"Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
	];

	/// <summary>
	/// Maps a 5-field cron expression (minute hour day-of-month month day-of-week) to an Azure Logic Apps
	/// recurrence trigger shape.
	/// </summary>
	/// <param name="cronExpression"> The cron expression to map. </param>
	/// <returns> The recurrence trigger shape to serialize into the workflow definition. </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="cronExpression"/> is null, empty, does not have exactly 5 fields, or is not a
	/// syntactically valid cron expression.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// <paramref name="cronExpression"/> is syntactically valid but cannot be represented as an Azure
	/// Logic Apps recurrence trigger (for example, it restricts day-of-month or month, or combines
	/// step/range/list values outside the supported patterns).
	/// </exception>
	public static AzureRecurrence Map(string cronExpression)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		var fields = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (fields.Length != 5)
		{
			throw new ArgumentException(
				$"Cron expression '{cronExpression}' must have exactly 5 fields (minute hour day-of-month month day-of-week).",
				nameof(cronExpression));
		}

		// Validate the expression is syntactically legal cron before attempting structural mapping, so a
		// genuinely malformed expression fails with a parse error rather than a misleading "not supported".
		try
		{
			_ = CronExpression.Parse(cronExpression, CronFormat.Standard);
		}
		catch (CronFormatException ex)
		{
			throw new ArgumentException($"'{cronExpression}' is not a valid cron expression.", nameof(cronExpression), ex);
		}

		var minuteField = fields[0];
		var hourField = fields[1];
		var domField = fields[2];
		var monthField = fields[3];
		var dowField = fields[4];

		if (domField != "*" || monthField != "*")
		{
			throw NotSupported(cronExpression);
		}

		// */N * * * *  -> every-N-minutes
		if (hourField == "*" && dowField == "*" && TryParseStep(minuteField, out var minuteStep))
		{
			return new AzureRecurrence("Minute", minuteStep, null, null, null);
		}

		// 0 */N * * *  -> every-N-hours
		if (minuteField == "0" && dowField == "*" && TryParseStep(hourField, out var hourStep))
		{
			return new AzureRecurrence("Hour", hourStep, null, null, null);
		}

		// M H * * *    -> daily at H:M
		if (dowField == "*" &&
			TryParseSingle(minuteField, 0, 59, out var dailyMinute) &&
			TryParseSingle(hourField, 0, 23, out var dailyHour))
		{
			return new AzureRecurrence("Day", 1, [dailyHour], [dailyMinute], null);
		}

		// M H * * <weekdays>  -> weekly at H:M on the given days
		if (TryParseSingle(minuteField, 0, 59, out var weeklyMinute) &&
			TryParseSingle(hourField, 0, 23, out var weeklyHour) &&
			TryParseWeekDays(dowField, out var weekDays))
		{
			return new AzureRecurrence("Week", 1, [weeklyHour], [weeklyMinute], weekDays);
		}

		throw NotSupported(cronExpression);
	}

	private static NotSupportedException NotSupported(string cronExpression) => new(
		$"Cron expression '{cronExpression}' cannot be represented as an Azure Logic Apps recurrence trigger. " +
		"Supported patterns: '*/N * * * *' (every N minutes), '0 */N * * *' (every N hours), " +
		"'M H * * *' (daily at H:M), and 'M H * * <weekdays>' (weekly at H:M on the given comma/range-separated weekdays).");

	private static bool TryParseStep(string field, out int step)
	{
		var match = StepPattern().Match(field);
		if (match.Success && int.TryParse(match.Groups[1].ValueSpan, out step) && step > 0)
		{
			return true;
		}

		step = 0;
		return false;
	}

	private static bool TryParseSingle(string field, int min, int max, out int value) =>
		int.TryParse(field, out value) && value >= min && value <= max;

	private static bool TryParseWeekDays(string field, out string[] weekDays)
	{
		weekDays = [];
		if (field == "*")
		{
			return false;
		}

		var days = new SortedSet<int>();
		foreach (var token in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var rangeMatch = RangePattern().Match(token);
			if (rangeMatch.Success)
			{
				if (!TryParseDayOfWeek(rangeMatch.Groups[1].Value, out var start) ||
					!TryParseDayOfWeek(rangeMatch.Groups[2].Value, out var end) ||
					start > end)
				{
					return false;
				}

				for (var day = start; day <= end; day++)
				{
					_ = days.Add(day);
				}

				continue;
			}

			if (!TryParseDayOfWeek(token, out var single))
			{
				return false;
			}

			_ = days.Add(single);
		}

		if (days.Count == 0)
		{
			return false;
		}

		weekDays = [.. days.Select(day => DayNames[day])];
		return true;
	}

	private static bool TryParseDayOfWeek(string token, out int day)
	{
		if (!int.TryParse(token, out day) || day is < 0 or > 7)
		{
			day = 0;
			return false;
		}

		// Cron allows both 0 and 7 for Sunday.
		if (day == 7)
		{
			day = 0;
		}

		return true;
	}

	[GeneratedRegex(@"^\*/(\d+)$")]
	private static partial Regex StepPattern();

	[GeneratedRegex(@"^(\d+)-(\d+)$")]
	private static partial Regex RangePattern();
}

/// <summary>
/// A cron expression mapped to the shape of an Azure Logic Apps recurrence trigger.
/// </summary>
/// <param name="Frequency"> The recurrence frequency ("Minute", "Hour", "Day", or "Week"). </param>
/// <param name="Interval"> The recurrence interval. </param>
/// <param name="Hours"> The hours of day the recurrence fires at, or <see langword="null"/> when not applicable. </param>
/// <param name="Minutes"> The minutes of the hour the recurrence fires at, or <see langword="null"/> when not applicable. </param>
/// <param name="WeekDays"> The days of the week the recurrence fires on, or <see langword="null"/> when not applicable. </param>
internal sealed record AzureRecurrence(string Frequency, int Interval, int[]? Hours, int[]? Minutes, string[]? WeekDays);
