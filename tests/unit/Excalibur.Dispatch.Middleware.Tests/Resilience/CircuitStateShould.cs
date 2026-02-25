// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="CircuitState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class CircuitStateShould
{
	[Fact]
	public void HaveCorrectValueForClosed()
	{
		// Assert
		((int)CircuitState.Closed).ShouldBe(0);
	}

	[Fact]
	public void HaveCorrectValueForOpen()
	{
		// Assert
		((int)CircuitState.Open).ShouldBe(1);
	}

	[Fact]
	public void HaveCorrectValueForHalfOpen()
	{
		// Assert
		((int)CircuitState.HalfOpen).ShouldBe(2);
	}

	[Fact]
	public void HaveExactlyThreeValues()
	{
		// Assert
		Enum.GetValues<CircuitState>().Length.ShouldBe(3);
	}

	[Fact]
	public void DefaultValueIsClosed()
	{
		// Arrange
		CircuitState defaultState = default;

		// Assert
		defaultState.ShouldBe(CircuitState.Closed);
	}

	[Theory]
	[InlineData(CircuitState.Closed, "Closed")]
	[InlineData(CircuitState.Open, "Open")]
	[InlineData(CircuitState.HalfOpen, "HalfOpen")]
	public void ConvertToCorrectStringRepresentation(CircuitState state, string expected)
	{
		// Assert
		state.ToString().ShouldBe(expected);
	}

	[Theory]
	[InlineData("Closed", CircuitState.Closed)]
	[InlineData("Open", CircuitState.Open)]
	[InlineData("HalfOpen", CircuitState.HalfOpen)]
	public void ParseFromStringCorrectly(string value, CircuitState expected)
	{
		// Act
		var result = Enum.Parse<CircuitState>(value);

		// Assert
		result.ShouldBe(expected);
	}
}
