// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Data.SqlServer.Scheduling;

public sealed partial class SqlServerScheduleStore
{
	// Finite provider contract validation, not an occurrence enumerator or custom
	// timezone resolver. Cronos remains owned by the scheduling subsystem.
	private static void ValidateCronRecurrence(IScheduleSpec schedule)
	{
		var absent = schedule.CronRecurrenceProfileId is null && schedule.CronRecurrenceProfileVersion is null;
		var known = string.Equals(schedule.CronRecurrenceProfileId, CronRecurrenceProfile.CronosCompatible.Id, StringComparison.Ordinal)
			&& schedule.CronRecurrenceProfileVersion == CronRecurrenceProfile.CronosCompatible.Version;
		if (!absent && !known)
		{
			throw new ArgumentException("Unknown or incomplete cron recurrence profile; explicitly select CronRecurrenceProfile.CronosCompatible.", nameof(schedule));
		}
		// Preserve opaque timezone metadata for one-time/elapsed-interval envelopes.
		if (string.IsNullOrWhiteSpace(schedule.CronExpression)) { return; }
		TimeZoneInfo zone;
		try { zone = schedule.TimeZoneId is null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId); }
		catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
		{
			throw new ArgumentException("The stored cron timezone must identify a valid installed zone; repair the retained configuration explicitly.", nameof(schedule), exception);
		}
		if (absent && !zone.Equals(TimeZoneInfo.Utc) && zone.GetAdjustmentRules().Length != 0)
		{
			throw new ArgumentException("A transition-zone recurrence requires an explicit CronRecurrenceProfile.CronosCompatible declaration.", nameof(schedule));
		}
	}
}
