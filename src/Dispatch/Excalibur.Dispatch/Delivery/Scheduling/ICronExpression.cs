// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Represents a parsed cron expression with timezone awareness.
/// </summary>
public interface ICronExpression
{
	/// <summary>
	/// Gets the original cron expression string.
	/// </summary>
	/// <value>
	/// The original cron expression string.
	/// </value>
	string Expression { get; }

	/// <summary>
	/// Gets the timezone for which this cron expression should be evaluated.
	/// </summary>
	/// <value>
	/// The timezone for which this cron expression should be evaluated.
	/// </value>
	TimeZoneInfo TimeZone { get; }

	/// <summary>
	/// Gets a value indicating whether the cron expression includes seconds field.
	/// </summary>
	/// <value>
	/// A value indicating whether the cron expression includes seconds field.
	/// </value>
	bool IncludesSeconds { get; }

	/// <summary>
	/// Calculates the next occurrence of the cron expression after the specified time.
	/// </summary>
	/// <param name="from"> The time from which to calculate the next occurrence. </param>
	/// <returns> The next occurrence time, or null if there is no next occurrence. </returns>
	DateTimeOffset? GetNextOccurrence(DateTimeOffset from);

	/// <summary>
	/// Validates whether the cron expression is valid.
	/// </summary>
	/// <returns> True if the expression is valid; otherwise, false. </returns>
	bool IsValid();

}
