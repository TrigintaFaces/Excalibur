// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Cronos;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// A timezone-aware wrapper around Cronos CronExpression.
/// </summary>
public sealed class TimeZoneAwareCronExpression : ICronExpression, ICronExpressionQuery
{
	private readonly CronExpression _cronExpression;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeZoneAwareCronExpression" /> class.
	/// </summary>
	/// <param name="expression"> The cron expression string. </param>
	/// <param name="timeZone"> The timezone for this expression. </param>
	/// <param name="includeSeconds">
	/// Optional hint. When <see langword="true"/>, forces 6-field (seconds-aware)
	/// parsing. When <see langword="false"/> (the default), the constructor
	/// auto-detects a 6-field expression by counting whitespace-separated fields
	/// so callers can pass either <c>"*/10 * * * *"</c> (5-field) or
	/// <c>"*/10 * * * * *"</c> (6-field) without first tuning
	/// <see cref="Excalibur.Dispatch.Options.Scheduling.CronScheduleOptions.IncludeSeconds"/>. [bd-rjj907]
	/// </param>
	public TimeZoneAwareCronExpression(string expression, TimeZoneInfo? timeZone = null, bool includeSeconds = false)
	{
		Expression = expression ?? throw new ArgumentNullException(nameof(expression));
		TimeZone = timeZone ?? TimeZoneInfo.Utc;

		// Auto-promote to seconds-aware parsing when the expression has 6
		// whitespace-separated fields. The explicit includeSeconds hint still
		// wins — pass true to force seconds parsing on a 5-field expression
		// (Cronos will reject, which is the desired behavior), or pass false
		// explicitly to accept only the 5-field case.
		IncludesSeconds = includeSeconds || HasSixFields(expression);

		var format = IncludesSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard;
		_cronExpression = CronExpression.Parse(expression, format);
	}

	/// <summary>
	/// Returns <see langword="true"/> when the cron expression has six
	/// whitespace-separated fields (Cronos seconds-aware form).
	/// </summary>
	/// <remarks>
	/// Cronos treats any single run of whitespace as a field separator. Named
	/// macros like <c>@daily</c> / <c>@every_second</c> are single-token and do
	/// not go through this path; CronExpression.Parse handles them directly.
	/// </remarks>
	private static bool HasSixFields(string expression)
	{
		if (string.IsNullOrWhiteSpace(expression) || expression.StartsWith('@'))
		{
			return false;
		}

		var fieldCount = 0;
		var inField = false;
		for (var i = 0; i < expression.Length; i++)
		{
			var ch = expression[i];
			if (char.IsWhiteSpace(ch))
			{
				if (inField)
				{
					fieldCount++;
					inField = false;
				}
			}
			else
			{
				inField = true;
			}
		}

		if (inField)
		{
			fieldCount++;
		}

		return fieldCount == 6;
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
		// Cronos requires Kind=Utc for the (DateTime, TimeZoneInfo) overload and
		// performs the timezone conversion internally. Use DateTimeOffset.UtcDateTime
		// which always returns a DateTime with Kind=Utc. [bd-61s6mw]
		var nextUtc = _cronExpression.GetNextOccurrence(from.UtcDateTime, TimeZone);

		if (nextUtc.HasValue)
		{
			// nextUtc is UTC; project into the expression's TZ offset at that instant.
			var offset = TimeZone.GetUtcOffset(nextUtc.Value);
			return new DateTimeOffset(nextUtc.Value, TimeSpan.Zero).ToOffset(offset);
		}

		return null;
	}

	/// <inheritdoc />
	public DateTimeOffset? GetNextOccurrenceUtc(DateTimeOffset fromUtc)
	{
		// Same Cronos contract as GetNextOccurrence: pass a Utc-kind DateTime and
		// Cronos handles the timezone transition internally. [bd-61s6mw]
		var nextUtc = _cronExpression.GetNextOccurrence(fromUtc.UtcDateTime, TimeZone);

		return nextUtc.HasValue
			? new DateTimeOffset(nextUtc.Value, TimeSpan.Zero)
			: null;
	}

	/// <inheritdoc />
	public IEnumerable<DateTimeOffset> GetOccurrencesBetween(DateTimeOffset from, DateTimeOffset endTime)
	{
		// Cronos GetOccurrences(DateTime, DateTime, TimeZoneInfo) requires Kind=Utc
		// on both bounds and returns Utc DateTimes. [bd-61s6mw]
		var occurrences = _cronExpression.GetOccurrences(from.UtcDateTime, endTime.UtcDateTime, TimeZone);

		foreach (var occurrence in occurrences)
		{
			yield return new DateTimeOffset(occurrence, TimeSpan.Zero).ToOffset(TimeZone.GetUtcOffset(occurrence));
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
