// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="SpecialConditions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class SpecialConditionsShould
{
	[Fact]
	public void HaveFalseBusinessDaysOnly_ByDefault()
	{
		// Arrange & Act
		var conditions = new SpecialConditions();

		// Assert
		conditions.BusinessDaysOnly.ShouldBeFalse();
	}

	[Fact]
	public void HaveFalseExcludeHolidays_ByDefault()
	{
		// Arrange & Act
		var conditions = new SpecialConditions();

		// Assert
		conditions.ExcludeHolidays.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptyCustomHolidays_ByDefault()
	{
		// Arrange & Act
		var conditions = new SpecialConditions();

		// Assert
		conditions.CustomHolidays.ShouldNotBeNull();
		conditions.CustomHolidays.ShouldBeEmpty();
	}

	[Fact]
	public void HaveFalsePeakHoursOnly_ByDefault()
	{
		// Arrange & Act
		var conditions = new SpecialConditions();

		// Assert
		conditions.PeakHoursOnly.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptyPeakHours_ByDefault()
	{
		// Arrange & Act
		var conditions = new SpecialConditions();

		// Assert
		conditions.PeakHours.ShouldNotBeNull();
		conditions.PeakHours.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingBusinessDaysOnly()
	{
		// Arrange
		var conditions = new SpecialConditions();

		// Act
		conditions.BusinessDaysOnly = true;

		// Assert
		conditions.BusinessDaysOnly.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingExcludeHolidays()
	{
		// Arrange
		var conditions = new SpecialConditions();

		// Act
		conditions.ExcludeHolidays = true;

		// Assert
		conditions.ExcludeHolidays.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingCustomHolidays()
	{
		// Arrange
		var conditions = new SpecialConditions();

		// Act
		conditions.CustomHolidays.Add(new DateOnly(2024, 12, 25)); // Christmas
		conditions.CustomHolidays.Add(new DateOnly(2024, 1, 1));   // New Year's Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 7, 4));   // Independence Day

		// Assert
		conditions.CustomHolidays.Count.ShouldBe(3);
		conditions.CustomHolidays.ShouldContain(new DateOnly(2024, 12, 25));
		conditions.CustomHolidays.ShouldContain(new DateOnly(2024, 1, 1));
		conditions.CustomHolidays.ShouldContain(new DateOnly(2024, 7, 4));
	}

	[Fact]
	public void AllowSettingPeakHoursOnly()
	{
		// Arrange
		var conditions = new SpecialConditions();

		// Act
		conditions.PeakHoursOnly = true;

		// Assert
		conditions.PeakHoursOnly.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingPeakHours()
	{
		// Arrange
		var conditions = new SpecialConditions();
		var morningPeak = new TimeRange
		{
			StartTime = TimeSpan.FromHours(8),
			EndTime = TimeSpan.FromHours(10),
		};
		var eveningPeak = new TimeRange
		{
			StartTime = TimeSpan.FromHours(17),
			EndTime = TimeSpan.FromHours(19),
		};

		// Act
		conditions.PeakHours.Add(morningPeak);
		conditions.PeakHours.Add(eveningPeak);

		// Assert
		conditions.PeakHours.Count.ShouldBe(2);
		conditions.PeakHours[0].StartTime.ShouldBe(TimeSpan.FromHours(8));
		conditions.PeakHours[1].StartTime.ShouldBe(TimeSpan.FromHours(17));
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var conditions = new SpecialConditions
		{
			BusinessDaysOnly = true,
			ExcludeHolidays = true,
			PeakHoursOnly = true,
		};

		conditions.CustomHolidays.Add(new DateOnly(2024, 12, 25));
		conditions.CustomHolidays.Add(new DateOnly(2024, 12, 26));

		conditions.PeakHours.Add(new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(11),
		});
		conditions.PeakHours.Add(new TimeRange
		{
			StartTime = TimeSpan.FromHours(14),
			EndTime = TimeSpan.FromHours(16),
		});

		// Assert
		conditions.BusinessDaysOnly.ShouldBeTrue();
		conditions.ExcludeHolidays.ShouldBeTrue();
		conditions.PeakHoursOnly.ShouldBeTrue();
		conditions.CustomHolidays.Count.ShouldBe(2);
		conditions.PeakHours.Count.ShouldBe(2);
	}

	[Fact]
	public void RepresentBusinessHoursConfiguration()
	{
		// Arrange & Act - Standard business hours config
		var conditions = new SpecialConditions
		{
			BusinessDaysOnly = true,
			ExcludeHolidays = true,
		};

		// US Federal Holidays 2024
		conditions.CustomHolidays.Add(new DateOnly(2024, 1, 1));   // New Year's Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 1, 15));  // MLK Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 2, 19));  // Presidents' Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 5, 27));  // Memorial Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 7, 4));   // Independence Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 9, 2));   // Labor Day
		conditions.CustomHolidays.Add(new DateOnly(2024, 11, 28)); // Thanksgiving
		conditions.CustomHolidays.Add(new DateOnly(2024, 12, 25)); // Christmas

		// Assert
		conditions.BusinessDaysOnly.ShouldBeTrue();
		conditions.ExcludeHolidays.ShouldBeTrue();
		conditions.CustomHolidays.Count.ShouldBe(8);
	}
}
