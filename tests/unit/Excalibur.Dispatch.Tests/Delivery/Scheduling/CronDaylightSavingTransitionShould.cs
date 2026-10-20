// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class CronDaylightSavingTransitionShould
{
	[Theory]
	[InlineData(30)]
	[InlineData(120)]
	public void MoveAGapTimeToTheFirstValidLocalInstant(int deltaMinutes)
	{
		var zone = CreateZone(deltaMinutes);
		var local = new DateTime(2026, 10, 4, 2, 15, 0, DateTimeKind.Unspecified).AddTicks(1234);
		zone.IsInvalidTime(local).ShouldBeTrue("the custom fixture must exercise an actual gap");
		var boundary = new DateTime(2026, 10, 4, 2, 0, 0, DateTimeKind.Unspecified).AddMinutes(deltaMinutes);
		zone.IsInvalidTime(boundary.AddTicks(-1)).ShouldBeTrue();
		zone.IsInvalidTime(boundary).ShouldBeFalse();
		var result = CreateScheduler().HandleDaylightSavingTransition(new DateTimeOffset(local, TimeSpan.Zero), zone);
		result.DateTime.ShouldBe(boundary);
		result.Offset.ShouldBe(TimeSpan.FromMinutes(630 + deltaMinutes));
	}

	[Theory]
	[InlineData(30, 2026, 4, 5, 1, 45, 660)]
	[InlineData(-30, 2026, 10, 4, 1, 45, 630)]
	public void ChooseTheEarliestInstantForAmbiguousWallTime(int delta, int year, int month, int day, int hour, int minute, int expectedOffset)
	{
		var zone = CreateZone(delta);
		var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
		zone.IsAmbiguousTime(local).ShouldBeTrue();
		var offsets = zone.GetAmbiguousTimeOffsets(local);
		offsets.Length.ShouldBe(2);
		var result = CreateScheduler().HandleDaylightSavingTransition(new DateTimeOffset(local, TimeSpan.Zero), zone);
		result.DateTime.ShouldBe(local);
		result.Offset.ShouldBe(TimeSpan.FromMinutes(expectedOffset));
		foreach (var offset in offsets)
		{
			result.UtcDateTime.ShouldBeLessThanOrEqualTo(new DateTimeOffset(local, offset).UtcDateTime);
		}
	}

	[Fact]
	public void ResolveTheGapAtTheEndOfANegativeDaylightAdjustment()
	{
		var zone = CreateZone(-30);
		var local = new DateTime(2026, 4, 5, 2, 15, 0, DateTimeKind.Unspecified);
		zone.IsInvalidTime(local).ShouldBeTrue();
		var result = CreateScheduler().HandleDaylightSavingTransition(new DateTimeOffset(local, TimeSpan.Zero), zone);
		result.ShouldBe(new DateTimeOffset(2026, 4, 5, 2, 30, 0, TimeSpan.FromMinutes(630)));
	}

	[Theory]
	[InlineData(false, 2, 15)]
	[InlineData(true, 4, 15)]
	public void PreserveTheOriginalOffsetAndWallFieldsWhenDisabledOrValid(bool enabled, int hour, int minute)
	{
		var zone = CreateZone(30);
		var input = new DateTimeOffset(2026, 10, 4, hour, minute, 0, TimeSpan.FromHours(-4));
		zone.IsInvalidTime(input.DateTime).ShouldBe(!enabled);
		var result = CreateScheduler(enabled).HandleDaylightSavingTransition(input, zone);
		result.EqualsExact(input).ShouldBeTrue();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void LeaveParsedCronEvaluationToCronosRegardlessOfTheHelperToggle(bool enabled)
	{
		var zone = CreateZone(30);
		var from = new DateTimeOffset(2026, 10, 4, 1, 59, 0, TimeSpan.FromMinutes(630));
		var result = CreateScheduler(enabled).Parse("15 2 * * *", zone).GetNextOccurrence(from);
		result.ShouldNotBeNull().ShouldBe(new DateTimeOffset(2026, 10, 4, 2, 30, 0, TimeSpan.FromMinutes(660)));
	}

	[Fact]
	public void StopAtTheValidIslandBetweenAdjacentRuleDates()
	{
		var firstDay = new DateTime(2026, 10, 4);
		var secondDay = firstDay.AddDays(1);
		var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 4, 1);
		var firstStart = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 23, 59, 0), 10, 4);
		var secondStart = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 5), 10, 5);
		var firstRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(firstDay, firstDay,
			TimeSpan.FromMinutes(30), firstStart, end);
		var secondRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(secondDay, secondDay,
			TimeSpan.FromMinutes(30), secondStart, end);
		var zone = TimeZoneInfo.CreateCustomTimeZone("R11-adjacent-rule-dates", TimeSpan.Zero,
			"Adjacent rule dates", "standard", "daylight", [firstRule, secondRule]);
		var input = firstDay.AddHours(23).AddMinutes(59).AddSeconds(50);
		zone.IsInvalidTime(input).ShouldBeTrue("the first rule must make the input invalid");
		zone.IsInvalidTime(secondDay).ShouldBeFalse("the next rule must leave a valid island at midnight");
		zone.IsInvalidTime(input.AddMinutes(1)).ShouldBeTrue("the first exponential probe must land in the second gap");
		var result = CreateScheduler().HandleDaylightSavingTransition(new DateTimeOffset(input, TimeSpan.Zero), zone);
		result.DateTime.ShouldBe(secondDay);
		result.Offset.ShouldBe(zone.GetUtcOffset(secondDay));
	}

	private static CronScheduler CreateScheduler(bool enabled = true) => new(
		Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { AutoAdjustForDaylightSaving = enabled }),
		NullLogger<CronScheduler>.Instance);

	private static TimeZoneInfo CreateZone(int daylightMinutes)
	{
		var atTwo = new DateTime(1, 1, 1, 2, 0, 0);
		var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 10, 1, DayOfWeek.Sunday);
		var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(atTwo, 4, 1, DayOfWeek.Sunday);
		var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2020, 1, 1),
			new DateTime(2030, 12, 31), TimeSpan.FromMinutes(daylightMinutes), start, end);
		return TimeZoneInfo.CreateCustomTimeZone($"R11-custom-{daylightMinutes}", TimeSpan.FromMinutes(630),
			"R11 deterministic transition fixture", "standard", "daylight", [rule]);
	}
}
