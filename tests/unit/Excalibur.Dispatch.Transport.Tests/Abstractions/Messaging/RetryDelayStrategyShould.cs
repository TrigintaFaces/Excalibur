// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="RetryDelayStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class RetryDelayStrategyShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<RetryDelayStrategy>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(RetryDelayStrategy.Fixed);
		values.ShouldContain(RetryDelayStrategy.Exponential);
		values.ShouldContain(RetryDelayStrategy.Linear);
	}

	[Fact]
	public void Fixed_HasExpectedValue()
	{
		// Assert
		((int)RetryDelayStrategy.Fixed).ShouldBe(0);
	}

	[Fact]
	public void Exponential_HasExpectedValue()
	{
		// Assert
		((int)RetryDelayStrategy.Exponential).ShouldBe(1);
	}

	[Fact]
	public void Linear_HasExpectedValue()
	{
		// Assert
		((int)RetryDelayStrategy.Linear).ShouldBe(2);
	}

	[Fact]
	public void Fixed_IsDefaultValue()
	{
		// Arrange
		RetryDelayStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(RetryDelayStrategy.Fixed);
	}

	[Theory]
	[InlineData(RetryDelayStrategy.Fixed)]
	[InlineData(RetryDelayStrategy.Exponential)]
	[InlineData(RetryDelayStrategy.Linear)]
	public void BeDefinedForAllValues(RetryDelayStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, RetryDelayStrategy.Fixed)]
	[InlineData(1, RetryDelayStrategy.Exponential)]
	[InlineData(2, RetryDelayStrategy.Linear)]
	public void CastFromInt_ReturnsCorrectValue(int value, RetryDelayStrategy expected)
	{
		// Act
		var strategy = (RetryDelayStrategy)value;

		// Assert
		strategy.ShouldBe(expected);
	}

	[Fact]
	public void Exponential_IsRecommendedForNetworkRetries()
	{
		// Assert - Exponential backoff is the recommended strategy for network errors
		Enum.IsDefined(RetryDelayStrategy.Exponential).ShouldBeTrue();
		((int)RetryDelayStrategy.Exponential).ShouldBe(1);
	}
}
