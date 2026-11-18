// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class CronFoldCharacterizationShould
{
    [Fact]
    public void FireAFixedWallTimeOnceAtTheEarlierFoldInstant()
    {
        var zone = FoldZone();
        AssertFold(zone);
        var actual = new TimeZoneAwareCronExpression("30 1 * * *", zone)
            .GetOccurrencesBetween(Start(), End()).ToArray();
        actual.ShouldBe([At(1, 30, 11)]);
    }

    [Fact]
    public void PreserveBothUtcSeriesForAnIntervalExpressionInStrictUtcOrder()
    {
        var zone = FoldZone();
        AssertFold(zone);
        var actual = new TimeZoneAwareCronExpression("*/30 1 * * *", zone)
            .GetOccurrencesBetween(Start(), End()).ToArray();
        actual.ShouldBe([At(1, 0, 11), At(1, 30, 11), At(1, 0, 10), At(1, 30, 10)]);
        actual.Select(x => x.UtcDateTime).Distinct().Count().ShouldBe(4);
        actual.Select(x => x.UtcDateTime).SequenceEqual(actual.Select(x => x.UtcDateTime).Order()).ShouldBeTrue();
    }

    [Fact]
    public void KeepTheTwoOrdinaryIntervalFiringsWithoutAClockTransition()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("R11-fold-fixed", TimeSpan.FromHours(10), "fixed", "fixed");
        var actual = new TimeZoneAwareCronExpression("*/30 1 * * *", zone)
            .GetOccurrencesBetween(At(0, 59, 10), End()).ToArray();
        actual.ShouldBe([At(1, 0, 10), At(1, 30, 10)]);
    }

    private static DateTimeOffset At(int hour, int minute, int offset) =>
        new(2026, 4, 5, hour, minute, 0, TimeSpan.FromHours(offset));
    private static DateTimeOffset Start() => At(0, 59, 11);
    private static DateTimeOffset End() => At(2, 1, 10);

    private static void AssertFold(TimeZoneInfo zone)
    {
        foreach (var minute in new[] { 0, 30 })
        {
            var local = new DateTime(2026, 4, 5, 1, minute, 0, DateTimeKind.Unspecified);
            zone.IsAmbiguousTime(local).ShouldBeTrue();
            zone.GetAmbiguousTimeOffsets(local).Order().ShouldBe([TimeSpan.FromHours(10), TimeSpan.FromHours(11)]);
        }
        zone.IsAmbiguousTime(new DateTime(2026, 4, 5, 2, 0, 0, DateTimeKind.Unspecified)).ShouldBeFalse();
    }

    private static TimeZoneInfo FoldZone()
    {
        var atTwo = new DateTime(1, 1, 1, 2, 0, 0);
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 10, 1, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 4, 1, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31), TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("R11-fold-characterization", TimeSpan.FromHours(10),
            "R11 deterministic fold", "standard", "daylight", [rule]);
    }
}
