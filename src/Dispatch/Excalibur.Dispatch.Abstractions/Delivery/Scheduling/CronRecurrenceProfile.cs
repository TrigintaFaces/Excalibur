// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>An explicit immutable declaration of supported local-time cron transition behavior.</summary>
/// <remarks>
/// Persist <see cref="Id"/> and <see cref="Version"/> with each recurrence. This declaration
/// does not pin timezone data, promise exactly-once effects, or alter catch-up policy.
/// Cronos remains the occurrence enumerator. Missing or unknown declarations must not
/// silently authorize new transition-zone occurrences. Already admitted UTC authority remains valid.
/// </remarks>
public sealed class CronRecurrenceProfile
{
    private CronRecurrenceProfile() { }

    /// <summary>Gets the supported Cronos-compatible transition declaration.</summary>
    /// <remarks>
    /// Gap aliases, including an ordinary boundary, coalesce to one UTC occurrence within
    /// one schedule. Fixed cron times fire at the earlier fold occurrence; interval cron
    /// expressions fire in both UTC series. Cronos classifies interval expressions using
    /// its time-field wildcard, range and step semantics, not merely fixed lists.
    /// Different schedule identifiers remain independent.
    /// </remarks>
    public static CronRecurrenceProfile CronosCompatible { get; } = new();

    /// <summary>Gets the primitive persisted profile identifier.</summary>
    public string Id => "cronos-compatible";

    /// <summary>Gets the persisted semantic version, distinct from timezone/package versions.</summary>
    public int Version => 1;

    /// <summary>Gets whether invalid local-time aliases coalesce at the next valid boundary.</summary>
    public bool CoalescesGapOccurrences => true;

    /// <summary>Gets whether a fixed cron time fires in both fold offsets; this profile uses the earlier offset only.</summary>
    public bool RepeatsFixedTimesInFold => false;

    /// <summary>Gets whether interval cron expressions retain both ordered UTC fold series.</summary>
    public bool RepeatsIntervalTimesInFold => true;
}
