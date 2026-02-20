// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="RateLimitAlgorithm"/> enum.
/// </summary>
/// <remarks>
/// Tests the rate limiting algorithm types.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class RateLimitAlgorithmShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveTokenBucketAsZero()
	{
		// Assert
		((int)RateLimitAlgorithm.TokenBucket).ShouldBe(0);
	}

	[Fact]
	public void HaveSlidingWindowAsOne()
	{
		// Assert
		((int)RateLimitAlgorithm.SlidingWindow).ShouldBe(1);
	}

	[Fact]
	public void HaveFixedWindowAsTwo()
	{
		// Assert
		((int)RateLimitAlgorithm.FixedWindow).ShouldBe(2);
	}

	[Fact]
	public void HaveConcurrencyAsThree()
	{
		// Assert
		((int)RateLimitAlgorithm.Concurrency).ShouldBe(3);
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Assert
		Enum.GetValues<RateLimitAlgorithm>().Length.ShouldBe(4);
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(RateLimitAlgorithm).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("TokenBucket")]
	[InlineData("SlidingWindow")]
	[InlineData("FixedWindow")]
	[InlineData("Concurrency")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<RateLimitAlgorithm>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("TokenBucket", RateLimitAlgorithm.TokenBucket)]
	[InlineData("SlidingWindow", RateLimitAlgorithm.SlidingWindow)]
	[InlineData("FixedWindow", RateLimitAlgorithm.FixedWindow)]
	[InlineData("Concurrency", RateLimitAlgorithm.Concurrency)]
	public void ParseFromString(string input, RateLimitAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<RateLimitAlgorithm>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowForInvalidParseInput()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<RateLimitAlgorithm>("InvalidAlgorithm"));
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("TokenBucket", RateLimitAlgorithm.TokenBucket)]
	[InlineData("SlidingWindow", RateLimitAlgorithm.SlidingWindow)]
	[InlineData("FixedWindow", RateLimitAlgorithm.FixedWindow)]
	[InlineData("Concurrency", RateLimitAlgorithm.Concurrency)]
	public void TryParseValidValues(string input, RateLimitAlgorithm expected)
	{
		// Act
		var success = Enum.TryParse<RateLimitAlgorithm>(input, out var result);

		// Assert
		success.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public void TryParseReturnsFalseForInvalidInput()
	{
		// Act
		var success = Enum.TryParse<RateLimitAlgorithm>("InvalidValue", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Conversion Tests

	[Theory]
	[InlineData(0, RateLimitAlgorithm.TokenBucket)]
	[InlineData(1, RateLimitAlgorithm.SlidingWindow)]
	[InlineData(2, RateLimitAlgorithm.FixedWindow)]
	[InlineData(3, RateLimitAlgorithm.Concurrency)]
	public void ConvertFromInt(int value, RateLimitAlgorithm expected)
	{
		// Act
		var result = (RateLimitAlgorithm)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(RateLimitAlgorithm.TokenBucket, 0)]
	[InlineData(RateLimitAlgorithm.SlidingWindow, 1)]
	[InlineData(RateLimitAlgorithm.FixedWindow, 2)]
	[InlineData(RateLimitAlgorithm.Concurrency, 3)]
	public void ConvertToInt(RateLimitAlgorithm algorithm, int expected)
	{
		// Act
		var result = (int)algorithm;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var algorithm = RateLimitAlgorithm.TokenBucket;

		// Act
		var description = algorithm switch
		{
			RateLimitAlgorithm.TokenBucket => "Smooth rate limiting",
			RateLimitAlgorithm.SlidingWindow => "Accurate rate limiting",
			RateLimitAlgorithm.FixedWindow => "Simple rate limiting",
			RateLimitAlgorithm.Concurrency => "Parallel execution limit",
			_ => "Unknown",
		};

		// Assert
		description.ShouldBe("Smooth rate limiting");
	}

	[Fact]
	public void DefaultValueIsTokenBucket()
	{
		// Act
		RateLimitAlgorithm defaultValue = default;

		// Assert
		defaultValue.ShouldBe(RateLimitAlgorithm.TokenBucket);
	}

	[Fact]
	public void AllAlgorithmsAreDifferent()
	{
		// Arrange
		var algorithms = Enum.GetValues<RateLimitAlgorithm>();

		// Act & Assert
		var distinctCount = algorithms.Distinct().Count();
		distinctCount.ShouldBe(algorithms.Length);
	}

	#endregion
}
