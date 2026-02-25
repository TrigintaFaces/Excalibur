// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="DateRange"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class DateRangeShould
{
	[Fact]
	public void HaveDefaultStartDate_ByDefault()
	{
		// Arrange & Act
		var range = new DateRange();

		// Assert
		range.StartDate.ShouldBe(default);
	}

	[Fact]
	public void HaveDefaultEndDate_ByDefault()
	{
		// Arrange & Act
		var range = new DateRange();

		// Assert
		range.EndDate.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingStartDate()
	{
		// Arrange
		var range = new DateRange();
		var startDate = new DateOnly(2024, 1, 1);

		// Act
		range.StartDate = startDate;

		// Assert
		range.StartDate.ShouldBe(startDate);
	}

	[Fact]
	public void AllowSettingEndDate()
	{
		// Arrange
		var range = new DateRange();
		var endDate = new DateOnly(2024, 12, 31);

		// Act
		range.EndDate = endDate;

		// Assert
		range.EndDate.ShouldBe(endDate);
	}

	[Fact]
	public void ContainDate_WhenDateIsWithinRange()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		};
		var testDate = new DateOnly(2024, 6, 15);

		// Act
		var result = range.Contains(testDate);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainDate_WhenDateEqualsStartDate()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		};

		// Act
		var result = range.Contains(new DateOnly(2024, 1, 1));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainDate_WhenDateEqualsEndDate()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		};

		// Act
		var result = range.Contains(new DateOnly(2024, 12, 31));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotContainDate_WhenDateIsBeforeRange()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		};

		// Act
		var result = range.Contains(new DateOnly(2023, 12, 31));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void NotContainDate_WhenDateIsAfterRange()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 1, 1),
			EndDate = new DateOnly(2024, 12, 31),
		};

		// Act
		var result = range.Contains(new DateOnly(2025, 1, 1));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainDate_WhenRangeIsSingleDay()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 6, 15),
			EndDate = new DateOnly(2024, 6, 15),
		};

		// Act
		var result = range.Contains(new DateOnly(2024, 6, 15));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotContainDate_WhenRangeIsSingleDay_AndDateIsDifferent()
	{
		// Arrange
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 6, 15),
			EndDate = new DateOnly(2024, 6, 15),
		};

		// Act
		var result = range.Contains(new DateOnly(2024, 6, 16));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var range = new DateRange
		{
			StartDate = new DateOnly(2024, 3, 1),
			EndDate = new DateOnly(2024, 3, 31),
		};

		// Assert
		range.StartDate.ShouldBe(new DateOnly(2024, 3, 1));
		range.EndDate.ShouldBe(new DateOnly(2024, 3, 31));
	}
}
