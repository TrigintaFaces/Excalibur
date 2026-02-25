// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="BranchingStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class BranchingStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveSimpleAsDefaultValue()
	{
		// Arrange & Act
		var strategy = default(BranchingStrategy);

		// Assert
		strategy.ShouldBe(BranchingStrategy.Simple);
	}

	[Fact]
	public void HaveSimpleEqualToZero()
	{
		// Arrange & Act
		var value = (int)BranchingStrategy.Simple;

		// Assert
		value.ShouldBe(0);
	}

	[Fact]
	public void HaveMultiWayEqualToOne()
	{
		// Arrange & Act
		var value = (int)BranchingStrategy.MultiWay;

		// Assert
		value.ShouldBe(1);
	}

	[Fact]
	public void HavePatternMatchingEqualToTwo()
	{
		// Arrange & Act
		var value = (int)BranchingStrategy.PatternMatching;

		// Assert
		value.ShouldBe(2);
	}

	[Fact]
	public void HaveStateMachineEqualToThree()
	{
		// Arrange & Act
		var value = (int)BranchingStrategy.StateMachine;

		// Assert
		value.ShouldBe(3);
	}

	#endregion Enum Value Tests

	#region Enum Definition Tests

	[Fact]
	public void ContainExactlyFourValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<BranchingStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("Simple")]
	[InlineData("MultiWay")]
	[InlineData("PatternMatching")]
	[InlineData("StateMachine")]
	public void ContainExpectedName(string expectedName)
	{
		// Arrange & Act
		var names = Enum.GetNames<BranchingStrategy>();

		// Assert
		names.ShouldContain(expectedName);
	}

	[Theory]
	[InlineData(BranchingStrategy.Simple)]
	[InlineData(BranchingStrategy.MultiWay)]
	[InlineData(BranchingStrategy.PatternMatching)]
	[InlineData(BranchingStrategy.StateMachine)]
	public void BeDefinedForAllValues(BranchingStrategy strategy)
	{
		// Act
		var isDefined = Enum.IsDefined(strategy);

		// Assert
		isDefined.ShouldBeTrue();
	}

	#endregion Enum Definition Tests

	#region Parsing Tests

	[Theory]
	[InlineData("Simple", BranchingStrategy.Simple)]
	[InlineData("MultiWay", BranchingStrategy.MultiWay)]
	[InlineData("PatternMatching", BranchingStrategy.PatternMatching)]
	[InlineData("StateMachine", BranchingStrategy.StateMachine)]
	public void ParseFromString(string input, BranchingStrategy expected)
	{
		// Act
		var result = Enum.Parse<BranchingStrategy>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("simple", BranchingStrategy.Simple)]
	[InlineData("MULTIWAY", BranchingStrategy.MultiWay)]
	[InlineData("patternmatching", BranchingStrategy.PatternMatching)]
	[InlineData("STATEMACHINE", BranchingStrategy.StateMachine)]
	public void ParseFromStringIgnoringCase(string input, BranchingStrategy expected)
	{
		// Act
		var result = Enum.Parse<BranchingStrategy>(input, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(BranchingStrategy.Simple, "Simple")]
	[InlineData(BranchingStrategy.MultiWay, "MultiWay")]
	[InlineData(BranchingStrategy.PatternMatching, "PatternMatching")]
	[InlineData(BranchingStrategy.StateMachine, "StateMachine")]
	public void ConvertToString(BranchingStrategy strategy, string expected)
	{
		// Act
		var result = strategy.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	#endregion Parsing Tests

	#region Casting Tests

	[Theory]
	[InlineData(0, BranchingStrategy.Simple)]
	[InlineData(1, BranchingStrategy.MultiWay)]
	[InlineData(2, BranchingStrategy.PatternMatching)]
	[InlineData(3, BranchingStrategy.StateMachine)]
	public void CastFromInt(int value, BranchingStrategy expected)
	{
		// Act
		var result = (BranchingStrategy)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(BranchingStrategy.Simple, 0)]
	[InlineData(BranchingStrategy.MultiWay, 1)]
	[InlineData(BranchingStrategy.PatternMatching, 2)]
	[InlineData(BranchingStrategy.StateMachine, 3)]
	public void CastToInt(BranchingStrategy strategy, int expected)
	{
		// Act
		var result = (int)strategy;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion Casting Tests
}
