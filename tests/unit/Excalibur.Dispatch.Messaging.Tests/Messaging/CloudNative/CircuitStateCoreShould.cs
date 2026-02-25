// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="CircuitState"/> enum in CloudNative namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class CircuitStateCoreShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<CircuitState>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(CircuitState.Closed);
		values.ShouldContain(CircuitState.Open);
		values.ShouldContain(CircuitState.HalfOpen);
	}

	[Fact]
	public void Closed_HasExpectedValue()
	{
		// Assert
		((int)CircuitState.Closed).ShouldBe(0);
	}

	[Fact]
	public void Open_HasExpectedValue()
	{
		// Assert
		((int)CircuitState.Open).ShouldBe(1);
	}

	[Fact]
	public void HalfOpen_HasExpectedValue()
	{
		// Assert
		((int)CircuitState.HalfOpen).ShouldBe(2);
	}

	[Fact]
	public void Closed_IsDefaultValue()
	{
		// Arrange
		CircuitState defaultState = default;

		// Assert
		defaultState.ShouldBe(CircuitState.Closed);
	}

	[Theory]
	[InlineData(CircuitState.Closed)]
	[InlineData(CircuitState.Open)]
	[InlineData(CircuitState.HalfOpen)]
	public void BeDefinedForAllValues(CircuitState state)
	{
		// Assert
		Enum.IsDefined(state).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, CircuitState.Closed)]
	[InlineData(1, CircuitState.Open)]
	[InlineData(2, CircuitState.HalfOpen)]
	public void CastFromInt_ReturnsCorrectValue(int value, CircuitState expected)
	{
		// Act
		var state = (CircuitState)value;

		// Assert
		state.ShouldBe(expected);
	}
}
