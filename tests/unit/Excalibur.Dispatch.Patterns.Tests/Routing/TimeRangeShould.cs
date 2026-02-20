// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="TimeRange"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class TimeRangeShould
{
	[Fact]
	public void HaveDefaultStartTime_ByDefault()
	{
		// Arrange & Act
		var range = new TimeRange();

		// Assert
		range.StartTime.ShouldBe(default);
	}

	[Fact]
	public void HaveDefaultEndTime_ByDefault()
	{
		// Arrange & Act
		var range = new TimeRange();

		// Assert
		range.EndTime.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingStartTime()
	{
		// Arrange
		var range = new TimeRange();
		var startTime = TimeSpan.FromHours(9);

		// Act
		range.StartTime = startTime;

		// Assert
		range.StartTime.ShouldBe(startTime);
	}

	[Fact]
	public void AllowSettingEndTime()
	{
		// Arrange
		var range = new TimeRange();
		var endTime = TimeSpan.FromHours(17);

		// Act
		range.EndTime = endTime;

		// Assert
		range.EndTime.ShouldBe(endTime);
	}

	[Fact]
	public void ContainTime_WhenTimeIsWithinRange()
	{
		// Arrange
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		};
		var testTime = TimeSpan.FromHours(12);

		// Act
		var result = range.Contains(testTime);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenTimeEqualsStartTime()
	{
		// Arrange
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(9));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenTimeEqualsEndTime()
	{
		// Arrange
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(17));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotContainTime_WhenTimeIsBeforeRange()
	{
		// Arrange
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(8));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void NotContainTime_WhenTimeIsAfterRange()
	{
		// Arrange
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(9),
			EndTime = TimeSpan.FromHours(17),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(18));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainTime_WhenRangeCrossesMidnight_AndTimeIsAfterStart()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act - 23:00 is after start
		var result = range.Contains(TimeSpan.FromHours(23));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenRangeCrossesMidnight_AndTimeIsBeforeEnd()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act - 03:00 is before end (next day)
		var result = range.Contains(TimeSpan.FromHours(3));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenRangeCrossesMidnight_AndTimeIsAtMidnight()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act - Midnight (00:00) is within the range
		var result = range.Contains(TimeSpan.Zero);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenRangeCrossesMidnight_AndTimeEqualsStartTime()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(22));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainTime_WhenRangeCrossesMidnight_AndTimeEqualsEndTime()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(6));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotContainTime_WhenRangeCrossesMidnight_AndTimeIsBetweenEndAndStart()
	{
		// Arrange - Night shift: 22:00 to 06:00
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(22),
			EndTime = TimeSpan.FromHours(6),
		};

		// Act - 12:00 is between end (06:00) and start (22:00)
		var result = range.Contains(TimeSpan.FromHours(12));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainTime_WhenRangeIsSameStartAndEnd()
	{
		// Arrange - Single point in time
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(12),
			EndTime = TimeSpan.FromHours(12),
		};

		// Act
		var result = range.Contains(TimeSpan.FromHours(12));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var range = new TimeRange
		{
			StartTime = TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(30)),
			EndTime = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30)),
		};

		// Assert
		range.StartTime.ShouldBe(new TimeSpan(8, 30, 0));
		range.EndTime.ShouldBe(new TimeSpan(17, 30, 0));
	}
}
