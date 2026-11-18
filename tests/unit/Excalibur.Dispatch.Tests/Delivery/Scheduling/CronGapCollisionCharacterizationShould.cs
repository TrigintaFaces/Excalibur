// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class CronGapCollisionCharacterizationShould
{
    [Theory]
    [InlineData("0 2 * * *")]
    [InlineData("30 2 * * *")]
    [InlineData("0 3 * * *")]
    public void MapEachIndependentFixedRecurrenceToTheSameBoundaryInstant(string expression)
    {
        var zone = GapZone();
        AssertGap(zone);
        var occurrences = new TimeZoneAwareCronExpression(expression, zone)
            .GetOccurrencesBetween(Start(), End()).ToArray();
        occurrences.ShouldBe([Boundary()]);
    }

    [Fact]
    public void CoalesceTheGapAndOrdinaryBoundaryWithinOneRecurrenceButKeepTheNextNormalFiring()
    {
        var zone = GapZone();
        AssertGap(zone);
        var occurrences = new TimeZoneAwareCronExpression("0,30 2,3 * * *", zone)
            .GetOccurrencesBetween(Start(), End().AddMinutes(30)).ToArray();
        occurrences.ShouldBe([Boundary(), Boundary().AddMinutes(30)]);
        occurrences.Select(x => x.UtcDateTime).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void KeepThreeSeparatelyRegisteredRecurrencesIndependentDespiteTheirEqualUtcInstants()
    {
        var zone = GapZone();
        AssertGap(zone);
        string[] expressions = ["0 2 * * *", "30 2 * * *", "0 3 * * *"];
        var populations = expressions.Select(expression => new TimeZoneAwareCronExpression(expression, zone)
            .GetOccurrencesBetween(Start(), End()).ToArray()).ToArray();
        populations.Length.ShouldBe(3);
        foreach (var population in populations) { population.ShouldBe([Boundary()]); }
        populations.Sum(x => x.Length).ShouldBe(3);
    }

    [Fact]
    public void PreserveAllFourNormalFiringsInAFixedOffsetZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("R11-fixed-offset", TimeSpan.FromHours(10),
            "R11 fixed", "R11 fixed");
        var from = new DateTimeOffset(2026, 10, 4, 1, 59, 0, TimeSpan.FromHours(10));
        var expected = new[] { from.AddMinutes(1), from.AddMinutes(31), from.AddMinutes(61), from.AddMinutes(91) };
        var occurrences = new TimeZoneAwareCronExpression("0,30 2,3 * * *", zone)
            .GetOccurrencesBetween(from, from.AddMinutes(92)).ToArray();
        occurrences.ShouldBe(expected);
    }

    private static DateTimeOffset Start() => new(2026, 10, 4, 1, 59, 0, TimeSpan.FromHours(10));
    private static DateTimeOffset Boundary() => new(2026, 10, 4, 3, 0, 0, TimeSpan.FromHours(11));
    private static DateTimeOffset End() => Boundary().AddMinutes(1);

    private static void AssertGap(TimeZoneInfo zone)
    {
        zone.IsInvalidTime(new DateTime(2026, 10, 4, 2, 0, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
        zone.IsInvalidTime(new DateTime(2026, 10, 4, 2, 30, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
        zone.IsInvalidTime(Boundary().DateTime.AddTicks(-1)).ShouldBeTrue();
        zone.IsInvalidTime(Boundary().DateTime).ShouldBeFalse();
    }

    private static TimeZoneInfo GapZone()
    {
        var atTwo = new DateTime(1, 1, 1, 2, 0, 0);
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 10, 1, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 4, 1, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31), TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("R11-gap-collision", TimeSpan.FromHours(10),
            "R11 deterministic gap", "standard", "daylight", [rule]);
    }
}
