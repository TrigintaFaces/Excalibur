// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Cronos;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// A timezone-aware wrapper around Cronos CronExpression.
/// </summary>
public sealed class TimeZoneAwareCronExpression : ICronExpression
{
	private readonly CronExpression _cronExpression;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeZoneAwareCronExpression" /> class.
	/// </summary>
	/// <param name="expression"> The cron expression string. </param>
	/// <param name="timeZone"> The timezone for this expression. </param>
	/// <param name="includeSeconds"> Whether the expression includes seconds. </param>
	public TimeZoneAwareCronExpression(string expression, TimeZoneInfo? timeZone = null, bool includeSeconds = false)
	{
		Expression = expression ?? throw new ArgumentNullException(nameof(expression));
		TimeZone = timeZone ?? TimeZoneInfo.Utc;
		IncludesSeconds = includeSeconds;

		// Parse with Cronos
		var format = includeSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard;
		_cronExpression = CronExpression.Parse(expression, format);
	}

	/// <inheritdoc />
	public string Expression { get; }

	/// <inheritdoc />
	public TimeZoneInfo TimeZone { get; }

	/// <inheritdoc />
	public bool IncludesSeconds { get; }

	/// <summary>
	/// Creates a TimeZoneAwareCronExpression from a standard cron string and timezone ID.
	/// </summary>
	/// <param name="expression"> The cron expression. </param>
	/// <param name="timeZoneId"> The timezone ID (e.g., "America/New_York"). </param>
	/// <param name="includeSeconds"> Whether the expression includes seconds. </param>
	/// <returns> A new TimeZoneAwareCronExpression instance. </returns>
	public static TimeZoneAwareCronExpression FromTimeZoneId(string expression, string timeZoneId, bool includeSeconds = false)
	{
		var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		return new TimeZoneAwareCronExpression(expression, timeZone, includeSeconds);
	}

	/// <inheritdoc />
	public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
	{
		// Convert to the expression's timezone
		var localTime = TimeZoneInfo.ConvertTime(from, TimeZone);
		var nextLocal = _cronExpression.GetNextOccurrence(localTime.DateTime, TimeZone);

		if (nextLocal.HasValue)
		{
			// Convert back to DateTimeOffset preserving the timezone
			return new DateTimeOffset(nextLocal.Value, TimeZone.GetUtcOffset(nextLocal.Value));
		}

		return null;
	}

	/// <inheritdoc />
	public DateTimeOffset? GetNextOccurrenceUtc(DateTimeOffset fromUtc)
	{
		// Convert UTC to expression's timezone
		var localTime = TimeZoneInfo.ConvertTimeFromUtc(fromUtc.UtcDateTime, TimeZone);
		var nextLocal = _cronExpression.GetNextOccurrence(localTime, TimeZone);

		if (nextLocal.HasValue)
		{
			// Convert back to UTC
			return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocal.Value, TimeZone), TimeSpan.Zero);
		}

		return null;
	}

	/// <inheritdoc />
	public IEnumerable<DateTimeOffset> GetOccurrencesBetween(DateTimeOffset from, DateTimeOffset endTime)
	{
		// Convert to expression's timezone
		var localFrom = TimeZoneInfo.ConvertTime(from, TimeZone);
		var localTo = TimeZoneInfo.ConvertTime(endTime, TimeZone);

		var occurrences = _cronExpression.GetOccurrences(localFrom.DateTime, localTo.DateTime, TimeZone);

		foreach (var occurrence in occurrences)
		{
			yield return new DateTimeOffset(occurrence, TimeZone.GetUtcOffset(occurrence));
		}
	}

	/// <inheritdoc />
	public bool IsValid()
	{
		try
		{
			// Try to get next occurrence to validate
			var next = GetNextOccurrence(DateTimeOffset.Now);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <inheritdoc />
	public string GetDescription()
	{
		// Provide a basic description - could be enhanced with a library like CronExpressionDescriptor
		var parts = new List<string>();

		if (IncludesSeconds)
		{
			parts.Add($"Cron (with seconds): {Expression}");
		}
		else
		{
			parts.Add($"Cron: {Expression}");
		}

		parts.Add($"Timezone: {TimeZone.DisplayName}");

		// Get next few occurrences for context
		var next = GetNextOccurrence(DateTimeOffset.Now);
		if (next.HasValue)
		{
			parts.Add($"Next run: {next.Value:yyyy-MM-dd HH:mm:ss zzz}");
		}

		return string.Join(" | ", parts);
	}
}
