// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Cronos;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of ICronScheduler with timezone awareness.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CronScheduler" /> class. </remarks>
/// <param name="options"> The cron schedule options. </param>
/// <param name="logger"> The logger. </param>
public sealed partial class CronScheduler(IOptions<CronScheduleOptions> options, ILogger<CronScheduler> logger) : ICronScheduler
{
	private readonly CronScheduleOptions _options = options.Value;

	/// <inheritdoc />
	public ICronExpression Parse(string expression, TimeZoneInfo? timeZone = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(expression);

		var tz = timeZone ?? _options.DefaultTimeZone;

		// Validate timezone if restrictions are configured
		if (_options.SupportedTimeZoneIds.Count > 0 && !_options.SupportedTimeZoneIds.Contains(tz.Id))
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.CronScheduler_UnsupportedTimeZone,
					tz.Id,
					string.Join(", ", _options.SupportedTimeZoneIds)),
				nameof(timeZone));
		}

		try
		{
			var cronExpression = new TimeZoneAwareCronExpression(expression, tz, _options.IncludeSeconds);

			if (_options.EnableDetailedLogging)
			{
				LogParsedCronExpression(expression, tz.Id);
			}

			return cronExpression;
		}
		catch (CronFormatException ex)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.InvariantCulture,
					Resources.CronScheduler_InvalidCronExpression,
					ex.Message),
				nameof(expression),
				ex);
		}
	}

	/// <inheritdoc />
	public bool TryParse(string expression, out ICronExpression? cronExpression) =>
		TryParse(expression, _options.DefaultTimeZone, out cronExpression);

	/// <inheritdoc />
	public bool TryParse(string expression, TimeZoneInfo timeZone, out ICronExpression? cronExpression)
	{
		cronExpression = null;

		if (string.IsNullOrWhiteSpace(expression))
		{
			return false;
		}

		try
		{
			cronExpression = Parse(expression, timeZone);
			return true;
		}
		catch (Exception ex)
		{
			LogFailedToParseCronExpression(expression, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public DateTimeOffset? GetPreviousOccurrence(ICronExpression cronExpression, DateTimeOffset from)
	{
		if (cronExpression is not TimeZoneAwareCronExpression timezoneAwareCronExpression)
		{
			throw new ArgumentException(
				Resources.CronScheduler_InvalidExpressionType,
				nameof(cronExpression));
		}

		// Cronos doesn't have GetPreviousOccurrence, so we need to work backwards This is a simplified implementation - for production,
		// consider a more efficient algorithm
		var searchStart = from.AddDays(-7); // Look back up to a week
		var occurrences = timezoneAwareCronExpression.GetOccurrencesBetween(searchStart, from);

		DateTimeOffset? lastOccurrence = null;
		foreach (var occurrence in occurrences)
		{
			if (occurrence < from)
			{
				lastOccurrence = occurrence;
			}
			else
			{
				break;
			}
		}

		return lastOccurrence;
	}

	/// <inheritdoc />
	public bool WouldTriggerAt(ICronExpression cronExpression, DateTimeOffset time)
	{
		// Check if the time matches the cron expression We look for the next occurrence from 1 second before the target time
		var checkFrom = time.AddSeconds(-1);
		var next = cronExpression.GetNextOccurrence(checkFrom);

		if (!next.HasValue)
		{
			return false;
		}

		// Check if the next occurrence is within the tolerance window
		var difference = Math.Abs((next.Value - time).TotalMilliseconds);
		return difference <= _options.ExecutionToleranceWindow.TotalMilliseconds;
	}

	/// <summary>
	/// Calculates missed executions for a cron expression.
	/// </summary>
	/// <param name="cronExpression"> The cron expression. </param>
	/// <param name="lastRun"> The last successful run time. </param>
	/// <param name="now"> The current time. </param>
	/// <returns> List of missed execution times. </returns>
	public IEnumerable<DateTimeOffset> GetMissedExecutions(ICronExpression cronExpression, DateTimeOffset lastRun, DateTimeOffset now)
	{
		if (lastRun >= now)
		{
			yield break;
		}

		var occurrences = cronExpression.GetOccurrencesBetween(lastRun, now);
		var count = 0;

		foreach (var occurrence in occurrences)
		{
			// Skip the exact lastRun time
			if (occurrence <= lastRun)
			{
				continue;
			}

			// Don't include future times
			if (occurrence > now)
			{
				break;
			}

			yield return occurrence;

			count++;
			if (count >= _options.MaxMissedExecutions)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Handles daylight saving time transitions for a scheduled time.
	/// </summary>
	/// <param name="scheduledTime"> The scheduled time. </param>
	/// <param name="timeZone"> The timezone. </param>
	/// <returns> Adjusted time if necessary. </returns>
	public DateTimeOffset HandleDaylightSavingTransition(DateTimeOffset scheduledTime, TimeZoneInfo timeZone)
	{
		if (!_options.AutoAdjustForDaylightSaving)
		{
			return scheduledTime;
		}

		try
		{
			// Check if the scheduled time is invalid due to DST
			var localTime = scheduledTime.DateTime;
			if (timeZone.IsInvalidTime(localTime))
			{
				// Time doesn't exist (spring forward) - move to the next valid time
				var adjusted = localTime.AddHours(1);
				LogAdjustedInvalidDstTime(scheduledTime, adjusted, timeZone.Id);
				return new DateTimeOffset(adjusted, timeZone.GetUtcOffset(adjusted));
			}

			// Check if the time is ambiguous (fall back)
			if (timeZone.IsAmbiguousTime(localTime))
			{
				// Prefer the first occurrence (before the transition)
				var offset = timeZone.GetUtcOffset(localTime);
				return new DateTimeOffset(localTime, offset);
			}
		}
		catch (Exception ex)
		{
			LogErrorHandlingDstTransition(scheduledTime, timeZone.Id, ex);
		}

		return scheduledTime;
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(DeliveryEventId.CronExpressionParsed, LogLevel.Debug,
		"Parsed cron expression '{Expression}' for timezone '{TimeZone}'")]
	private partial void LogParsedCronExpression(string expression, string timeZone);

	[LoggerMessage(DeliveryEventId.CronExpressionParseFailed, LogLevel.Debug,
		"Failed to parse cron expression '{Expression}'")]
	private partial void LogFailedToParseCronExpression(string expression, Exception ex);

	[LoggerMessage(DeliveryEventId.CronDstAdjustment, LogLevel.Information,
		"Adjusted invalid DST time from {Original} to {Adjusted} in timezone {TimeZone}")]
	private partial void LogAdjustedInvalidDstTime(DateTimeOffset original, DateTime adjusted, string timeZone);

	[LoggerMessage(DeliveryEventId.CronDstTransitionError, LogLevel.Warning,
		"Error handling DST transition for time {Time} in timezone {TimeZone}")]
	private partial void LogErrorHandlingDstTransition(DateTimeOffset time, string timeZone, Exception ex);
}
