// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="JitterStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class JitterStrategyShould
{
	[Fact]
	public void HaveCorrectValueForNone()
	{
		// Assert
		((int)JitterStrategy.None).ShouldBe(0);
	}

	[Fact]
	public void HaveCorrectValueForFull()
	{
		// Assert
		((int)JitterStrategy.Full).ShouldBe(1);
	}

	[Fact]
	public void HaveCorrectValueForEqual()
	{
		// Assert
		((int)JitterStrategy.Equal).ShouldBe(2);
	}

	[Fact]
	public void HaveCorrectValueForDecorrelated()
	{
		// Assert
		((int)JitterStrategy.Decorrelated).ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectValueForExponential()
	{
		// Assert
		((int)JitterStrategy.Exponential).ShouldBe(4);
	}

	[Fact]
	public void HaveExactlyFiveValues()
	{
		// Assert
		Enum.GetValues<JitterStrategy>().Length.ShouldBe(5);
	}

	[Fact]
	public void DefaultValueIsNone()
	{
		// Arrange
		JitterStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(JitterStrategy.None);
	}

	[Theory]
	[InlineData(JitterStrategy.None, "None")]
	[InlineData(JitterStrategy.Full, "Full")]
	[InlineData(JitterStrategy.Equal, "Equal")]
	[InlineData(JitterStrategy.Decorrelated, "Decorrelated")]
	[InlineData(JitterStrategy.Exponential, "Exponential")]
	public void ConvertToCorrectStringRepresentation(JitterStrategy strategy, string expected)
	{
		// Assert
		strategy.ToString().ShouldBe(expected);
	}

	[Theory]
	[InlineData("None", JitterStrategy.None)]
	[InlineData("Full", JitterStrategy.Full)]
	[InlineData("Equal", JitterStrategy.Equal)]
	[InlineData("Decorrelated", JitterStrategy.Decorrelated)]
	[InlineData("Exponential", JitterStrategy.Exponential)]
	public void ParseFromStringCorrectly(string value, JitterStrategy expected)
	{
		// Act
		var result = Enum.Parse<JitterStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
