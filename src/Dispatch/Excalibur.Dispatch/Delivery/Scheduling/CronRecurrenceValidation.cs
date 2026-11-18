// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>Validates explicit cron declarations without changing occurrence enumeration.</summary>
internal static class CronRecurrenceValidation
{
    internal static TimeZoneInfo Validate(IScheduleSpec schedule)
    {
        var absent = schedule.CronRecurrenceProfileId is null && schedule.CronRecurrenceProfileVersion is null;
        var known = string.Equals(schedule.CronRecurrenceProfileId, CronRecurrenceProfile.CronosCompatible.Id, StringComparison.Ordinal)
            && schedule.CronRecurrenceProfileVersion == CronRecurrenceProfile.CronosCompatible.Version;
        if (!absent && !known)
        {
            throw new ArgumentException("Unknown or incomplete cron recurrence profile; explicitly select CronRecurrenceProfile.CronosCompatible.", nameof(schedule));
        }
        // One-time/elapsed interval schedules do not interpret opaque timezone envelope metadata.
        if (string.IsNullOrWhiteSpace(schedule.CronExpression)) { return TimeZoneInfo.Utc; }
        var zone = ResolveStoredTimeZone(schedule.TimeZoneId);
        if (absent && !zone.Equals(TimeZoneInfo.Utc) && zone.GetAdjustmentRules().Length != 0)
        {
            throw new ArgumentException("A transition-zone recurrence requires an explicit CronRecurrenceProfile.CronosCompatible declaration.", nameof(schedule));
        }
        return zone;
    }

    internal static TimeZoneInfo ResolveStoredTimeZone(string? id)
    {
        if (id is null) { return TimeZoneInfo.Utc; }
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new ArgumentException("The stored cron timezone must identify a valid installed zone; repair the retained configuration explicitly.", nameof(id), exception);
        }
    }

    internal static TimeZoneInfo ResolveSuppliedTimeZone(TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var installed = ResolveStoredTimeZone(timeZone.Id);
        if (!timeZone.HasSameRules(installed))
        {
            throw new ArgumentException("The supplied timezone rules differ from its installed identifier; custom rules cannot roundtrip through a persisted schedule.", nameof(timeZone));
        }
        return installed;
    }
}
