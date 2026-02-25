// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="ParallelismStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ParallelismStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveUnlimitedAsDefaultValue()
	{
		// Arrange & Act
		var strategy = default(ParallelismStrategy);

		// Assert
		strategy.ShouldBe(ParallelismStrategy.Unlimited);
	}

	[Fact]
	public void HaveUnlimitedEqualToZero()
	{
		// Arrange & Act
		var value = (int)ParallelismStrategy.Unlimited;

		// Assert
		value.ShouldBe(0);
	}

	[Fact]
	public void HaveLimitedEqualToOne()
	{
		// Arrange & Act
		var value = (int)ParallelismStrategy.Limited;

		// Assert
		value.ShouldBe(1);
	}

	[Fact]
	public void HaveBatchedEqualToTwo()
	{
		// Arrange & Act
		var value = (int)ParallelismStrategy.Batched;

		// Assert
		value.ShouldBe(2);
	}

	[Fact]
	public void HaveAdaptiveEqualToThree()
	{
		// Arrange & Act
		var value = (int)ParallelismStrategy.Adaptive;

		// Assert
		value.ShouldBe(3);
	}

	#endregion Enum Value Tests

	#region Enum Definition Tests

	[Fact]
	public void ContainExactlyFourValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<ParallelismStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("Unlimited")]
	[InlineData("Limited")]
	[InlineData("Batched")]
	[InlineData("Adaptive")]
	public void ContainExpectedName(string expectedName)
	{
		// Arrange & Act
		var names = Enum.GetNames<ParallelismStrategy>();

		// Assert
		names.ShouldContain(expectedName);
	}

	[Theory]
	[InlineData(ParallelismStrategy.Unlimited)]
	[InlineData(ParallelismStrategy.Limited)]
	[InlineData(ParallelismStrategy.Batched)]
	[InlineData(ParallelismStrategy.Adaptive)]
	public void BeDefinedForAllValues(ParallelismStrategy strategy)
	{
		// Act
		var isDefined = Enum.IsDefined(strategy);

		// Assert
		isDefined.ShouldBeTrue();
	}

	#endregion Enum Definition Tests

	#region Parsing Tests

	[Theory]
	[InlineData("Unlimited", ParallelismStrategy.Unlimited)]
	[InlineData("Limited", ParallelismStrategy.Limited)]
	[InlineData("Batched", ParallelismStrategy.Batched)]
	[InlineData("Adaptive", ParallelismStrategy.Adaptive)]
	public void ParseFromString(string input, ParallelismStrategy expected)
	{
		// Act
		var result = Enum.Parse<ParallelismStrategy>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("unlimited", ParallelismStrategy.Unlimited)]
	[InlineData("LIMITED", ParallelismStrategy.Limited)]
	[InlineData("batched", ParallelismStrategy.Batched)]
	[InlineData("ADAPTIVE", ParallelismStrategy.Adaptive)]
	public void ParseFromStringIgnoringCase(string input, ParallelismStrategy expected)
	{
		// Act
		var result = Enum.Parse<ParallelismStrategy>(input, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(ParallelismStrategy.Unlimited, "Unlimited")]
	[InlineData(ParallelismStrategy.Limited, "Limited")]
	[InlineData(ParallelismStrategy.Batched, "Batched")]
	[InlineData(ParallelismStrategy.Adaptive, "Adaptive")]
	public void ConvertToString(ParallelismStrategy strategy, string expected)
	{
		// Act
		var result = strategy.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	#endregion Parsing Tests

	#region Casting Tests

	[Theory]
	[InlineData(0, ParallelismStrategy.Unlimited)]
	[InlineData(1, ParallelismStrategy.Limited)]
	[InlineData(2, ParallelismStrategy.Batched)]
	[InlineData(3, ParallelismStrategy.Adaptive)]
	public void CastFromInt(int value, ParallelismStrategy expected)
	{
		// Act
		var result = (ParallelismStrategy)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(ParallelismStrategy.Unlimited, 0)]
	[InlineData(ParallelismStrategy.Limited, 1)]
	[InlineData(ParallelismStrategy.Batched, 2)]
	[InlineData(ParallelismStrategy.Adaptive, 3)]
	public void CastToInt(ParallelismStrategy strategy, int expected)
	{
		// Act
		var result = (int)strategy;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion Casting Tests
}
