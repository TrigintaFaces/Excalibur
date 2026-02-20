// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="CloudEventsMode"/> enum.
/// </summary>
/// <remarks>
/// Tests the Cloud Events processing modes.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class CloudEventsModeShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveNoneAsZero()
	{
		// Assert
		((int)CloudEventsMode.None).ShouldBe(0);
	}

	[Fact]
	public void HaveStructuredAsOne()
	{
		// Assert
		((int)CloudEventsMode.Structured).ShouldBe(1);
	}

	[Fact]
	public void HaveBinaryAsTwo()
	{
		// Assert
		((int)CloudEventsMode.Binary).ShouldBe(2);
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Assert
		Enum.GetValues<CloudEventsMode>().Length.ShouldBe(3);
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(CloudEventsMode).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("None")]
	[InlineData("Structured")]
	[InlineData("Binary")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<CloudEventsMode>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("None", CloudEventsMode.None)]
	[InlineData("Structured", CloudEventsMode.Structured)]
	[InlineData("Binary", CloudEventsMode.Binary)]
	public void ParseFromString(string input, CloudEventsMode expected)
	{
		// Act
		var result = Enum.Parse<CloudEventsMode>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowForInvalidParseInput()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<CloudEventsMode>("InvalidMode"));
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("None", CloudEventsMode.None)]
	[InlineData("Structured", CloudEventsMode.Structured)]
	[InlineData("Binary", CloudEventsMode.Binary)]
	public void TryParseValidValues(string input, CloudEventsMode expected)
	{
		// Act
		var success = Enum.TryParse<CloudEventsMode>(input, out var result);

		// Assert
		success.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public void TryParseReturnsFalseForInvalidInput()
	{
		// Act
		var success = Enum.TryParse<CloudEventsMode>("InvalidValue", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Conversion Tests

	[Theory]
	[InlineData(0, CloudEventsMode.None)]
	[InlineData(1, CloudEventsMode.Structured)]
	[InlineData(2, CloudEventsMode.Binary)]
	public void ConvertFromInt(int value, CloudEventsMode expected)
	{
		// Act
		var result = (CloudEventsMode)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(CloudEventsMode.None, 0)]
	[InlineData(CloudEventsMode.Structured, 1)]
	[InlineData(CloudEventsMode.Binary, 2)]
	public void ConvertToInt(CloudEventsMode mode, int expected)
	{
		// Act
		var result = (int)mode;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var mode = CloudEventsMode.Structured;

		// Act
		var description = mode switch
		{
			CloudEventsMode.None => "No Cloud Events",
			CloudEventsMode.Structured => "Structured JSON",
			CloudEventsMode.Binary => "Binary content mode",
			_ => "Unknown",
		};

		// Assert
		description.ShouldBe("Structured JSON");
	}

	[Fact]
	public void DefaultValueIsNone()
	{
		// Act
		CloudEventsMode defaultValue = default;

		// Assert
		defaultValue.ShouldBe(CloudEventsMode.None);
	}

	[Fact]
	public void AllModesAreDifferent()
	{
		// Arrange
		var modes = Enum.GetValues<CloudEventsMode>();

		// Act & Assert
		var distinctCount = modes.Distinct().Count();
		distinctCount.ShouldBe(modes.Length);
	}

	#endregion
}
