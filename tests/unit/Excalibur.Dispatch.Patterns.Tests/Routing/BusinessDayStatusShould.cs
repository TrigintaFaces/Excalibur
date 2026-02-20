// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="BusinessDayStatus"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class BusinessDayStatusShould
{
	[Fact]
	public void HaveFalseIsBusinessDay_ByDefault()
	{
		// Arrange & Act
		var status = new BusinessDayStatus();

		// Assert
		status.IsBusinessDay.ShouldBeFalse();
	}

	[Fact]
	public void HaveFalseIsBusinessHours_ByDefault()
	{
		// Arrange & Act
		var status = new BusinessDayStatus();

		// Assert
		status.IsBusinessHours.ShouldBeFalse();
	}

	[Fact]
	public void HaveFalseIsHoliday_ByDefault()
	{
		// Arrange & Act
		var status = new BusinessDayStatus();

		// Assert
		status.IsHoliday.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullHolidayName_ByDefault()
	{
		// Arrange & Act
		var status = new BusinessDayStatus();

		// Assert
		status.HolidayName.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultNextBusinessDay_ByDefault()
	{
		// Arrange & Act
		var status = new BusinessDayStatus();

		// Assert
		status.NextBusinessDay.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingIsBusinessDay()
	{
		// Arrange
		var status = new BusinessDayStatus();

		// Act
		status.IsBusinessDay = true;

		// Assert
		status.IsBusinessDay.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingIsBusinessHours()
	{
		// Arrange
		var status = new BusinessDayStatus();

		// Act
		status.IsBusinessHours = true;

		// Assert
		status.IsBusinessHours.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingIsHoliday()
	{
		// Arrange
		var status = new BusinessDayStatus();

		// Act
		status.IsHoliday = true;

		// Assert
		status.IsHoliday.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingHolidayName()
	{
		// Arrange
		var status = new BusinessDayStatus();

		// Act
		status.HolidayName = "Christmas Day";

		// Assert
		status.HolidayName.ShouldBe("Christmas Day");
	}

	[Fact]
	public void AllowSettingNextBusinessDay()
	{
		// Arrange
		var status = new BusinessDayStatus();
		var nextBusinessDay = new DateOnly(2024, 12, 26);

		// Act
		status.NextBusinessDay = nextBusinessDay;

		// Assert
		status.NextBusinessDay.ShouldBe(nextBusinessDay);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var status = new BusinessDayStatus
		{
			IsBusinessDay = false,
			IsBusinessHours = false,
			IsHoliday = true,
			HolidayName = "New Year's Day",
			NextBusinessDay = new DateOnly(2024, 1, 2),
		};

		// Assert
		status.IsBusinessDay.ShouldBeFalse();
		status.IsBusinessHours.ShouldBeFalse();
		status.IsHoliday.ShouldBeTrue();
		status.HolidayName.ShouldBe("New Year's Day");
		status.NextBusinessDay.ShouldBe(new DateOnly(2024, 1, 2));
	}

	[Fact]
	public void RepresentWorkingBusinessDay()
	{
		// Arrange & Act
		var status = new BusinessDayStatus
		{
			IsBusinessDay = true,
			IsBusinessHours = true,
			IsHoliday = false,
			HolidayName = null,
			NextBusinessDay = new DateOnly(2024, 6, 18),
		};

		// Assert
		status.IsBusinessDay.ShouldBeTrue();
		status.IsBusinessHours.ShouldBeTrue();
		status.IsHoliday.ShouldBeFalse();
		status.HolidayName.ShouldBeNull();
	}

	[Fact]
	public void RepresentWeekendDay()
	{
		// Arrange & Act
		var status = new BusinessDayStatus
		{
			IsBusinessDay = false,
			IsBusinessHours = false,
			IsHoliday = false,
			NextBusinessDay = new DateOnly(2024, 6, 17), // Monday after weekend
		};

		// Assert
		status.IsBusinessDay.ShouldBeFalse();
		status.IsBusinessHours.ShouldBeFalse();
		status.IsHoliday.ShouldBeFalse();
	}
}
