// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="MiddlewareRateLimitAlgorithm"/> enum.
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
		((int)MiddlewareRateLimitAlgorithm.TokenBucket).ShouldBe(0);
	}

	[Fact]
	public void HaveSlidingWindowAsOne()
	{
		// Assert
		((int)MiddlewareRateLimitAlgorithm.SlidingWindow).ShouldBe(1);
	}

	[Fact]
	public void HaveFixedWindowAsTwo()
	{
		// Assert
		((int)MiddlewareRateLimitAlgorithm.FixedWindow).ShouldBe(2);
	}

	[Fact]
	public void HaveConcurrencyAsThree()
	{
		// Assert
		((int)MiddlewareRateLimitAlgorithm.Concurrency).ShouldBe(3);
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Assert
		Enum.GetValues<MiddlewareRateLimitAlgorithm>().Length.ShouldBe(4);
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(MiddlewareRateLimitAlgorithm).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("TokenBucket")]
	[InlineData("SlidingWindow")]
	[InlineData("FixedWindow")]
	[InlineData("Concurrency")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<MiddlewareRateLimitAlgorithm>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("TokenBucket", MiddlewareRateLimitAlgorithm.TokenBucket)]
	[InlineData("SlidingWindow", MiddlewareRateLimitAlgorithm.SlidingWindow)]
	[InlineData("FixedWindow", MiddlewareRateLimitAlgorithm.FixedWindow)]
	[InlineData("Concurrency", MiddlewareRateLimitAlgorithm.Concurrency)]
	public void ParseFromString(string input, MiddlewareRateLimitAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<MiddlewareRateLimitAlgorithm>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowForInvalidParseInput()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<MiddlewareRateLimitAlgorithm>("InvalidAlgorithm"));
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("TokenBucket", MiddlewareRateLimitAlgorithm.TokenBucket)]
	[InlineData("SlidingWindow", MiddlewareRateLimitAlgorithm.SlidingWindow)]
	[InlineData("FixedWindow", MiddlewareRateLimitAlgorithm.FixedWindow)]
	[InlineData("Concurrency", MiddlewareRateLimitAlgorithm.Concurrency)]
	public void TryParseValidValues(string input, MiddlewareRateLimitAlgorithm expected)
	{
		// Act
		var success = Enum.TryParse<MiddlewareRateLimitAlgorithm>(input, out var result);

		// Assert
		success.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public void TryParseReturnsFalseForInvalidInput()
	{
		// Act
		var success = Enum.TryParse<MiddlewareRateLimitAlgorithm>("InvalidValue", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Conversion Tests

	[Theory]
	[InlineData(0, MiddlewareRateLimitAlgorithm.TokenBucket)]
	[InlineData(1, MiddlewareRateLimitAlgorithm.SlidingWindow)]
	[InlineData(2, MiddlewareRateLimitAlgorithm.FixedWindow)]
	[InlineData(3, MiddlewareRateLimitAlgorithm.Concurrency)]
	public void ConvertFromInt(int value, MiddlewareRateLimitAlgorithm expected)
	{
		// Act
		var result = (MiddlewareRateLimitAlgorithm)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(MiddlewareRateLimitAlgorithm.TokenBucket, 0)]
	[InlineData(MiddlewareRateLimitAlgorithm.SlidingWindow, 1)]
	[InlineData(MiddlewareRateLimitAlgorithm.FixedWindow, 2)]
	[InlineData(MiddlewareRateLimitAlgorithm.Concurrency, 3)]
	public void ConvertToInt(MiddlewareRateLimitAlgorithm algorithm, int expected)
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
		var algorithm = MiddlewareRateLimitAlgorithm.TokenBucket;

		// Act
		var description = algorithm switch
		{
			MiddlewareRateLimitAlgorithm.TokenBucket => "Smooth rate limiting",
			MiddlewareRateLimitAlgorithm.SlidingWindow => "Accurate rate limiting",
			MiddlewareRateLimitAlgorithm.FixedWindow => "Simple rate limiting",
			MiddlewareRateLimitAlgorithm.Concurrency => "Parallel execution limit",
			_ => "Unknown",
		};

		// Assert
		description.ShouldBe("Smooth rate limiting");
	}

	[Fact]
	public void DefaultValueIsTokenBucket()
	{
		// Act
		MiddlewareRateLimitAlgorithm defaultValue = default;

		// Assert
		defaultValue.ShouldBe(MiddlewareRateLimitAlgorithm.TokenBucket);
	}

	[Fact]
	public void AllAlgorithmsAreDifferent()
	{
		// Arrange
		var algorithms = Enum.GetValues<MiddlewareRateLimitAlgorithm>();

		// Act & Assert
		var distinctCount = algorithms.Distinct().Count();
		distinctCount.ShouldBe(algorithms.Length);
	}

	#endregion
}
