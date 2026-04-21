// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Extension methods for <see cref="ICronExpression"/>.
/// </summary>
public static class CronExpressionExtensions
{
	/// <summary>Calculates the next occurrence in UTC.</summary>
	public static DateTimeOffset? GetNextOccurrenceUtc(this ICronExpression cron, DateTimeOffset fromUtc)
	{
		ArgumentNullException.ThrowIfNull(cron);
		if (cron is ICronExpressionQuery query)
		{
			return query.GetNextOccurrenceUtc(fromUtc);
		}

		// Fallback: use the non-UTC method
		return cron.GetNextOccurrence(fromUtc);
	}

	/// <summary>Gets all occurrences between the specified times.</summary>
	public static IEnumerable<DateTimeOffset> GetOccurrencesBetween(this ICronExpression cron, DateTimeOffset from, DateTimeOffset endTime)
	{
		ArgumentNullException.ThrowIfNull(cron);
		if (cron is ICronExpressionQuery query)
		{
			return query.GetOccurrencesBetween(from, endTime);
		}

		return [];
	}

	/// <summary>Gets a human-readable description of the cron expression.</summary>
	public static string GetDescription(this ICronExpression cron)
	{
		ArgumentNullException.ThrowIfNull(cron);
		if (cron is ICronExpressionQuery query)
		{
			return query.GetDescription();
		}

		return cron.Expression;
	}
}
