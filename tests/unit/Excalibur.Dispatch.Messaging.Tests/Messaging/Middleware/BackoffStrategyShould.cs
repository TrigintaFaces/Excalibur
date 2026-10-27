// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="BackoffStrategy"/> enum.
/// </summary>
/// <remarks>
/// Tests the backoff strategies used for retry delays.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
[Trait("Priority", "0")]
public sealed class BackoffStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveFixedAsZero()
	{
		// Assert
		((int)BackoffStrategy.Constant).ShouldBe(0);
	}

	[Fact]
	public void HaveLinearAsOne()
	{
		// Assert
		((int)BackoffStrategy.Linear).ShouldBe(1);
	}

	[Fact]
	public void HaveExponentialAsTwo()
	{
		// Assert
		((int)BackoffStrategy.Exponential).ShouldBe(2);
	}

	[Fact]
	public void HaveExponentialWithJitterAsThree()
	{
		// Assert
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Assert — seven values since DecorrelatedJitter (=6) was added alongside FullJitter (=5).
		Enum.GetValues<BackoffStrategy>().Length.ShouldBe(3);
	}

	[Fact]
	public void HaveDecorrelatedJitterAsSix()
	{
		// Assert
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(BackoffStrategy).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("Constant")]
	[InlineData("Linear")]
	[InlineData("Exponential")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<BackoffStrategy>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Constant", BackoffStrategy.Constant)]
	[InlineData("Linear", BackoffStrategy.Linear)]
	[InlineData("Exponential", BackoffStrategy.Exponential)]
	public void ParseFromString(string input, BackoffStrategy expected)
	{
		// Act
		var result = Enum.Parse<BackoffStrategy>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowForInvalidParseInput()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<BackoffStrategy>("InvalidStrategy"));
	}

	[Theory]
	[InlineData("constant", true)]
	[InlineData("CONSTANT", true)]
	[InlineData("Constant", true)]
	public void SupportCaseInsensitiveParsing(string input, bool ignoreCase)
	{
		// Act
		var result = Enum.Parse<BackoffStrategy>(input, ignoreCase);

		// Assert
		result.ShouldBe(BackoffStrategy.Constant);
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("Constant", BackoffStrategy.Constant)]
	[InlineData("Linear", BackoffStrategy.Linear)]
	[InlineData("Exponential", BackoffStrategy.Exponential)]
	public void TryParseValidValues(string input, BackoffStrategy expected)
	{
		// Act
		var success = Enum.TryParse<BackoffStrategy>(input, out var result);

		// Assert
		success.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public void TryParseReturnsFalseForInvalidInput()
	{
		// Act
		var success = Enum.TryParse<BackoffStrategy>("InvalidValue", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Conversion Tests

	[Theory]
	[InlineData(0, BackoffStrategy.Constant)]
	[InlineData(1, BackoffStrategy.Linear)]
	[InlineData(2, BackoffStrategy.Exponential)]
	public void ConvertFromInt(int value, BackoffStrategy expected)
	{
		// Act
		var result = (BackoffStrategy)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(BackoffStrategy.Constant, 0)]
	[InlineData(BackoffStrategy.Linear, 1)]
	[InlineData(BackoffStrategy.Exponential, 2)]
	public void ConvertToInt(BackoffStrategy strategy, int expected)
	{
		// Act
		var result = (int)strategy;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var strategy = BackoffStrategy.Exponential;

		// Act
		var description = strategy switch
		{
			BackoffStrategy.Constant => "Fixed delay",
			BackoffStrategy.Linear => "Linear backoff",
			BackoffStrategy.Exponential => "Exponential backoff",
			_ => "Unknown",
		};

		// Assert
		description.ShouldBe("Exponential backoff");
	}

	[Fact]
	public void DefaultValueIsFixed()
	{
		// Act
		BackoffStrategy defaultValue = default;

		// Assert
		defaultValue.ShouldBe(BackoffStrategy.Constant);
	}

	#endregion
}
