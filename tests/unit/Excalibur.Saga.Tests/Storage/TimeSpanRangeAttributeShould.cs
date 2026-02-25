// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="TimeSpanRangeAttribute"/>.
/// Verifies TimeSpan validation range behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class TimeSpanRangeAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void ParseMinimumAndMaximum_FromStrings()
	{
		// Act
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Assert
		sut.Minimum.ShouldBe(TimeSpan.FromSeconds(1));
		sut.Maximum.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void ParseComplexTimeSpanFormats()
	{
		// Act
		var sut = new TimeSpanRangeAttribute("00:05:30", "02:30:45");

		// Assert
		sut.Minimum.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30));
		sut.Maximum.ShouldBe(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void ThrowFormatException_WhenMinimumIsInvalid()
	{
		// Act & Assert
		Should.Throw<FormatException>(() =>
			new TimeSpanRangeAttribute("invalid", "01:00:00"));
	}

	[Fact]
	public void ThrowFormatException_WhenMaximumIsInvalid()
	{
		// Act & Assert
		Should.Throw<FormatException>(() =>
			new TimeSpanRangeAttribute("00:00:01", "invalid"));
	}

	#endregion

	#region IsValid Tests - Valid Values

	[Fact]
	public void ReturnTrue_WhenValueIsNull()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(null);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_WhenValueIsWithinRange()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.FromMinutes(30));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_WhenValueEqualsMinimum()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.FromSeconds(1));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_WhenValueEqualsMaximum()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.FromHours(1));

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region IsValid Tests - Invalid Values

	[Fact]
	public void ReturnFalse_WhenValueIsBelowMinimum()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.Zero);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_WhenValueExceedsMaximum()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.FromHours(2));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_WhenValueIsNotTimeSpan()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid("not a timespan");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_WhenValueIsInteger()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(100);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_WhenValueIsNegative()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.IsValid(TimeSpan.FromSeconds(-1));

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region FormatErrorMessage Tests

	[Fact]
	public void FormatErrorMessage_IncludesFieldName()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.FormatErrorMessage("TestField");

		// Assert
		result.ShouldContain("TestField");
	}

	[Fact]
	public void FormatErrorMessage_IncludesMinimumValue()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.FormatErrorMessage("TestField");

		// Assert
		result.ShouldContain("0:00:01");
	}

	[Fact]
	public void FormatErrorMessage_IncludesMaximumValue()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:01", "01:00:00");

		// Act
		var result = sut.FormatErrorMessage("TestField");

		// Assert
		result.ShouldContain("1:00:00");
	}

	[Fact]
	public void FormatErrorMessage_HasProperStructure()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:05:00", "02:00:00");

		// Act
		var result = sut.FormatErrorMessage("Timeout");

		// Assert
		result.ShouldContain("Timeout");
		result.ShouldContain("between");
		result.ShouldContain("05:00");
		result.ShouldContain("02:00:00");
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void HandleZeroMinimum()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:00", "01:00:00");

		// Act
		var resultZero = sut.IsValid(TimeSpan.Zero);
		var resultNegative = sut.IsValid(TimeSpan.FromSeconds(-1));

		// Assert
		resultZero.ShouldBeTrue();
		resultNegative.ShouldBeFalse();
	}

	[Fact]
	public void HandleDaysInTimeSpan()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:00:00", "1.00:00:00");

		// Act
		var resultWithinDay = sut.IsValid(TimeSpan.FromHours(12));
		var resultExactDay = sut.IsValid(TimeSpan.FromDays(1));
		var resultOverDay = sut.IsValid(TimeSpan.FromDays(2));

		// Assert
		resultWithinDay.ShouldBeTrue();
		resultExactDay.ShouldBeTrue();
		resultOverDay.ShouldBeFalse();
	}

	[Fact]
	public void HandleSameMinAndMax()
	{
		// Arrange
		var sut = new TimeSpanRangeAttribute("00:30:00", "00:30:00");

		// Act
		var resultExact = sut.IsValid(TimeSpan.FromMinutes(30));
		var resultSlightlyLess = sut.IsValid(TimeSpan.FromMinutes(29));
		var resultSlightlyMore = sut.IsValid(TimeSpan.FromMinutes(31));

		// Assert
		resultExact.ShouldBeTrue();
		resultSlightlyLess.ShouldBeFalse();
		resultSlightlyMore.ShouldBeFalse();
	}

	#endregion
}
