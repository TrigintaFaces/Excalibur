// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitAlgorithm"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitAlgorithmShould
{
	[Fact]
	public void HaveUnknownAsZero()
	{
		// Assert
		((int)RateLimitAlgorithm.Unknown).ShouldBe(0);
	}

	[Fact]
	public void HaveTokenBucketAsOne()
	{
		// Assert
		((int)RateLimitAlgorithm.TokenBucket).ShouldBe(1);
	}

	[Fact]
	public void HaveSlidingWindowAsTwo()
	{
		// Assert
		((int)RateLimitAlgorithm.SlidingWindow).ShouldBe(2);
	}

	[Fact]
	public void HaveFixedWindowAsThree()
	{
		// Assert
		((int)RateLimitAlgorithm.FixedWindow).ShouldBe(3);
	}

	[Fact]
	public void HaveConcurrencyAsFour()
	{
		// Assert
		((int)RateLimitAlgorithm.Concurrency).ShouldBe(4);
	}

	[Fact]
	public void HaveFiveDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<RateLimitAlgorithm>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void DefaultToUnknown()
	{
		// Arrange & Act
		var defaultValue = default(RateLimitAlgorithm);

		// Assert
		defaultValue.ShouldBe(RateLimitAlgorithm.Unknown);
	}

	[Theory]
	[InlineData(RateLimitAlgorithm.Unknown, "Unknown")]
	[InlineData(RateLimitAlgorithm.TokenBucket, "TokenBucket")]
	[InlineData(RateLimitAlgorithm.SlidingWindow, "SlidingWindow")]
	[InlineData(RateLimitAlgorithm.FixedWindow, "FixedWindow")]
	[InlineData(RateLimitAlgorithm.Concurrency, "Concurrency")]
	public void HaveCorrectStringRepresentation(RateLimitAlgorithm algorithm, string expected)
	{
		// Act
		var result = algorithm.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Unknown", RateLimitAlgorithm.Unknown)]
	[InlineData("TokenBucket", RateLimitAlgorithm.TokenBucket)]
	[InlineData("SlidingWindow", RateLimitAlgorithm.SlidingWindow)]
	[InlineData("FixedWindow", RateLimitAlgorithm.FixedWindow)]
	[InlineData("Concurrency", RateLimitAlgorithm.Concurrency)]
	public void ParseFromString(string value, RateLimitAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<RateLimitAlgorithm>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
