// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="TimeConditions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class TimeConditionsShould
{
	[Fact]
	public void HaveEmptyActiveTimeRanges_ByDefault()
	{
		// Arrange & Act
		var conditions = new TimeConditions();

		// Assert
		conditions.ActiveTimeRanges.ShouldNotBeNull();
		conditions.ActiveTimeRanges.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyActiveDaysOfWeek_ByDefault()
	{
		// Arrange & Act
		var conditions = new TimeConditions();

		// Assert
		conditions.ActiveDaysOfWeek.ShouldNotBeNull();
		conditions.ActiveDaysOfWeek.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyActiveDates_ByDefault()
	{
		// Arrange & Act
		var conditions = new TimeConditions();

		// Assert
		conditions.ActiveDates.ShouldNotBeNull();
		conditions.ActiveDates.ShouldBeEmpty();
	}

	[Fact]
	public void HaveLocalTimeZoneId_ByDefault()
	{
		// Arrange & Act
		var conditions = new TimeConditions();

		// Assert
		conditions.TimeZoneId.ShouldBe(TimeZoneInfo.Local.Id);
	}

	[Fact]
	public void HaveNullSpecialConditions_ByDefault()
	{
		// Arrange & Act
		var conditions = new TimeConditions();

		// Assert
		conditions.SpecialConditions.ShouldBeNull();
	}

	[Fact]
	public void AllowAddingActiveTimeRanges()
	{
		// Arrange
		var conditions = new TimeConditions();
		var morningRange = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(12),
		};
		var afternoonRange = new TimeRange
		{
			StartTime = TimeSpan.FromHours(13),
			EndTime = TimeSpan.FromHours(17),
		};

		// Act
		conditions.ActiveTimeRanges.Add(morningRange);
		conditions.ActiveTimeRanges.Add(afternoonRange);

		// Assert
		conditions.ActiveTimeRanges.Count.ShouldBe(2);
		conditions.ActiveTimeRanges[0].StartTime.ShouldBe(TimeSpan.FromHours(9));
		conditions.ActiveTimeRanges[1].StartTime.ShouldBe(TimeSpan.FromHours(13));
	}

	[Fact]
	public void AllowAddingActiveDaysOfWeek()
	{
		// Arrange
		var conditions = new TimeConditions();

		// Act
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Monday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Tuesday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Wednesday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Thursday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Friday);

		// Assert
		conditions.ActiveDaysOfWeek.Count.ShouldBe(5);
		conditions.ActiveDaysOfWeek.ShouldContain(DayOfWeek.Monday);
		conditions.ActiveDaysOfWeek.ShouldContain(DayOfWeek.Friday);
		conditions.ActiveDaysOfWeek.ShouldNotContain(DayOfWeek.Saturday);
		conditions.ActiveDaysOfWeek.ShouldNotContain(DayOfWeek.Sunday);
	}

	[Fact]
	public void AllowAddingActiveDates()
	{
		// Arrange
		var conditions = new TimeConditions();
		var q1Range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 3, 31),
		};
		var q2Range = new DateRange
		{
			StartDate = new DateOnly(2024, 4, 1),
			EndDate = new DateOnly(2024, 6, 30),
		};

		// Act
		conditions.ActiveDates.Add(q1Range);
		conditions.ActiveDates.Add(q2Range);

		// Assert
		conditions.ActiveDates.Count.ShouldBe(2);
		conditions.ActiveDates[0].StartDate.ShouldBe(new DateOnly(2024, 1, 1));
		conditions.ActiveDates[1].StartDate.ShouldBe(new DateOnly(2024, 4, 1));
	}

	[Fact]
	public void AllowSettingTimeZoneId()
	{
		// Arrange
		var conditions = new TimeConditions();

		// Act
		conditions.TimeZoneId = "America/New_York";

		// Assert
		conditions.TimeZoneId.ShouldBe("America/New_York");
	}

	[Fact]
	public void AllowSettingSpecialConditions()
	{
		// Arrange
		var conditions = new TimeConditions();
		var special = new SpecialConditions
		{
			BusinessDaysOnly = true,
			ExcludeHolidays = true,
		};

		// Act
		conditions.SpecialConditions = special;

		// Assert
		conditions.SpecialConditions.ShouldNotBeNull();
		conditions.SpecialConditions.BusinessDaysOnly.ShouldBeTrue();
		conditions.SpecialConditions.ExcludeHolidays.ShouldBeTrue();
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var conditions = new TimeConditions
		{
			TimeZoneId = "UTC",
			SpecialConditions = new SpecialConditions
			{
				BusinessDaysOnly = true,
				PeakHoursOnly = true,
			},
		};

		conditions.ActiveTimeRanges.Add(new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		});

		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Monday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Wednesday);
		conditions.ActiveDaysOfWeek.Add(DayOfWeek.Friday);

		conditions.ActiveDates.Add(new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		});

		// Assert
		conditions.TimeZoneId.ShouldBe("UTC");
		conditions.SpecialConditions.ShouldNotBeNull();
		conditions.SpecialConditions.BusinessDaysOnly.ShouldBeTrue();
		conditions.ActiveTimeRanges.Count.ShouldBe(1);
		conditions.ActiveDaysOfWeek.Count.ShouldBe(3);
		conditions.ActiveDates.Count.ShouldBe(1);
	}
}
