// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="StalePositionRecoveryStrategy"/> enum.
/// Tests the stale position recovery strategy enumeration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StalePositionRecoveryStrategyShould : UnitTestBase
{
	[Fact]
	public void Throw_HasCorrectValue()
	{
		// Assert
		((int)StalePositionRecoveryStrategy.Throw).ShouldBe(0);
	}

	[Fact]
	public void FallbackToEarliest_HasCorrectValue()
	{
		// Assert
		((int)StalePositionRecoveryStrategy.FallbackToEarliest).ShouldBe(1);
	}

	[Fact]
	public void FallbackToLatest_HasCorrectValue()
	{
		// Assert
		((int)StalePositionRecoveryStrategy.FallbackToLatest).ShouldBe(2);
	}

	[Fact]
	public void InvokeCallback_HasCorrectValue()
	{
		// Assert
		((int)StalePositionRecoveryStrategy.InvokeCallback).ShouldBe(3);
	}

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<StalePositionRecoveryStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(StalePositionRecoveryStrategy.Throw, "Throw")]
	[InlineData(StalePositionRecoveryStrategy.FallbackToEarliest, "FallbackToEarliest")]
	[InlineData(StalePositionRecoveryStrategy.FallbackToLatest, "FallbackToLatest")]
	[InlineData(StalePositionRecoveryStrategy.InvokeCallback, "InvokeCallback")]
	public void ToString_ReturnsCorrectName(StalePositionRecoveryStrategy strategy, string expectedName)
	{
		// Act
		var result = strategy.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	[Theory]
	[InlineData("Throw", StalePositionRecoveryStrategy.Throw)]
	[InlineData("FallbackToEarliest", StalePositionRecoveryStrategy.FallbackToEarliest)]
	[InlineData("FallbackToLatest", StalePositionRecoveryStrategy.FallbackToLatest)]
	[InlineData("InvokeCallback", StalePositionRecoveryStrategy.InvokeCallback)]
	public void Parse_ReturnsCorrectValue(string name, StalePositionRecoveryStrategy expectedValue)
	{
		// Act
		var result = Enum.Parse<StalePositionRecoveryStrategy>(name);

		// Assert
		result.ShouldBe(expectedValue);
	}

	[Fact]
	public void DefaultValue_IsThrow()
	{
		// This test documents that the default value is Throw (value 0)
		// even though the CdcOptions defaults to FallbackToEarliest
		StalePositionRecoveryStrategy defaultValue = default;
		defaultValue.ShouldBe(StalePositionRecoveryStrategy.Throw);
	}
}
