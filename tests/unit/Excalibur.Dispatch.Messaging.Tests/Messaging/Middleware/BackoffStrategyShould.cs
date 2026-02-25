// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="BackoffStrategy"/> enum.
/// </summary>
/// <remarks>
/// Tests the backoff strategies used for retry delays.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class BackoffStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveFixedAsZero()
	{
		// Assert
		((int)BackoffStrategy.Fixed).ShouldBe(0);
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
		((int)BackoffStrategy.ExponentialWithJitter).ShouldBe(3);
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveFiveDefinedValues()
	{
		// Assert
		Enum.GetValues<BackoffStrategy>().Length.ShouldBe(5);
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(BackoffStrategy).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("Fixed")]
	[InlineData("Linear")]
	[InlineData("Exponential")]
	[InlineData("ExponentialWithJitter")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<BackoffStrategy>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Fixed", BackoffStrategy.Fixed)]
	[InlineData("Linear", BackoffStrategy.Linear)]
	[InlineData("Exponential", BackoffStrategy.Exponential)]
	[InlineData("ExponentialWithJitter", BackoffStrategy.ExponentialWithJitter)]
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
	[InlineData("fixed", true)]
	[InlineData("FIXED", true)]
	[InlineData("Fixed", true)]
	public void SupportCaseInsensitiveParsing(string input, bool ignoreCase)
	{
		// Act
		var result = Enum.Parse<BackoffStrategy>(input, ignoreCase);

		// Assert
		result.ShouldBe(BackoffStrategy.Fixed);
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("Fixed", BackoffStrategy.Fixed)]
	[InlineData("Linear", BackoffStrategy.Linear)]
	[InlineData("Exponential", BackoffStrategy.Exponential)]
	[InlineData("ExponentialWithJitter", BackoffStrategy.ExponentialWithJitter)]
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
	[InlineData(0, BackoffStrategy.Fixed)]
	[InlineData(1, BackoffStrategy.Linear)]
	[InlineData(2, BackoffStrategy.Exponential)]
	[InlineData(3, BackoffStrategy.ExponentialWithJitter)]
	public void ConvertFromInt(int value, BackoffStrategy expected)
	{
		// Act
		var result = (BackoffStrategy)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(BackoffStrategy.Fixed, 0)]
	[InlineData(BackoffStrategy.Linear, 1)]
	[InlineData(BackoffStrategy.Exponential, 2)]
	[InlineData(BackoffStrategy.ExponentialWithJitter, 3)]
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
			BackoffStrategy.Fixed => "Fixed delay",
			BackoffStrategy.Linear => "Linear backoff",
			BackoffStrategy.Exponential => "Exponential backoff",
			BackoffStrategy.ExponentialWithJitter => "Exponential with jitter",
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
		defaultValue.ShouldBe(BackoffStrategy.Fixed);
	}

	#endregion
}
