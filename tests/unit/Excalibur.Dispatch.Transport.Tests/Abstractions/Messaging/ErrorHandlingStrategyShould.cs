// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="ErrorHandlingStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ErrorHandlingStrategyShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ErrorHandlingStrategy>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(ErrorHandlingStrategy.Retry);
		values.ShouldContain(ErrorHandlingStrategy.DeadLetter);
		values.ShouldContain(ErrorHandlingStrategy.Ignore);
		values.ShouldContain(ErrorHandlingStrategy.Throw);
	}

	[Fact]
	public void Retry_HasExpectedValue()
	{
		// Assert
		((int)ErrorHandlingStrategy.Retry).ShouldBe(0);
	}

	[Fact]
	public void DeadLetter_HasExpectedValue()
	{
		// Assert
		((int)ErrorHandlingStrategy.DeadLetter).ShouldBe(1);
	}

	[Fact]
	public void Ignore_HasExpectedValue()
	{
		// Assert
		((int)ErrorHandlingStrategy.Ignore).ShouldBe(2);
	}

	[Fact]
	public void Throw_HasExpectedValue()
	{
		// Assert
		((int)ErrorHandlingStrategy.Throw).ShouldBe(3);
	}

	[Fact]
	public void Retry_IsDefaultValue()
	{
		// Arrange
		ErrorHandlingStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(ErrorHandlingStrategy.Retry);
	}

	[Theory]
	[InlineData(ErrorHandlingStrategy.Retry)]
	[InlineData(ErrorHandlingStrategy.DeadLetter)]
	[InlineData(ErrorHandlingStrategy.Ignore)]
	[InlineData(ErrorHandlingStrategy.Throw)]
	public void BeDefinedForAllValues(ErrorHandlingStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ErrorHandlingStrategy.Retry)]
	[InlineData(1, ErrorHandlingStrategy.DeadLetter)]
	[InlineData(2, ErrorHandlingStrategy.Ignore)]
	[InlineData(3, ErrorHandlingStrategy.Throw)]
	public void CastFromInt_ReturnsCorrectValue(int value, ErrorHandlingStrategy expected)
	{
		// Act
		var strategy = (ErrorHandlingStrategy)value;

		// Assert
		strategy.ShouldBe(expected);
	}

	[Fact]
	public void Retry_IsSafestDefaultStrategy()
	{
		// Assert - Retry is the default and safest strategy for transient failures
		var defaultStrategy = default(ErrorHandlingStrategy);
		defaultStrategy.ShouldBe(ErrorHandlingStrategy.Retry);
	}
}
