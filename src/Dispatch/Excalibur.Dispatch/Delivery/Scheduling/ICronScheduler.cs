// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides cron-based scheduling capabilities with timezone awareness.
/// </summary>
public interface ICronScheduler
{
	/// <summary>
	/// Parses a cron expression string into an ICronExpression.
	/// </summary>
	/// <param name="expression"> The cron expression string. </param>
	/// <param name="timeZone"> The timezone for evaluating the expression. If null, uses the system's local timezone. </param>
	/// <returns> A parsed cron expression. </returns>
	ICronExpression Parse(string expression, TimeZoneInfo? timeZone = null);

	/// <summary>
	/// Validates whether a cron expression string is valid.
	/// </summary>
	/// <param name="expression"> The cron expression to validate. </param>
	/// <param name="cronExpression">
	/// When this method returns, contains the parsed cron expression if the input is valid; otherwise, null.
	/// </param>
	/// <returns> True if the expression is valid; otherwise, false. </returns>
	bool TryParse(string expression, out ICronExpression? cronExpression);

	/// <summary>
	/// Validates whether a cron expression string is valid for a specific timezone.
	/// </summary>
	/// <param name="expression"> The cron expression to validate. </param>
	/// <param name="timeZone"> The timezone for the expression. </param>
	/// <param name="cronExpression"> The parsed expression if valid. </param>
	/// <returns> True if the expression is valid; otherwise, false. </returns>
	bool TryParse(string expression, TimeZoneInfo timeZone, out ICronExpression? cronExpression);

	/// <summary>
	/// Calculates when a cron expression should have last run.
	/// </summary>
	/// <param name="cronExpression"> The cron expression. </param>
	/// <param name="from"> The time from which to calculate backwards. </param>
	/// <returns> The previous occurrence time, or null if there is no previous occurrence. </returns>
	DateTimeOffset? GetPreviousOccurrence(ICronExpression cronExpression, DateTimeOffset from);

	/// <summary>
	/// Determines if a cron expression would trigger at a specific time.
	/// </summary>
	/// <param name="cronExpression"> The cron expression. </param>
	/// <param name="time"> The time to check. </param>
	/// <returns> True if the expression would trigger at the specified time. </returns>
	bool WouldTriggerAt(ICronExpression cronExpression, DateTimeOffset time);
}
