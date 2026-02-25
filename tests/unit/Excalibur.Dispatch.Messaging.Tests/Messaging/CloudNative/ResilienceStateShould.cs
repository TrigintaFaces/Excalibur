// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="ResilienceState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ResilienceStateShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ResilienceState>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(ResilienceState.Closed);
		values.ShouldContain(ResilienceState.Open);
		values.ShouldContain(ResilienceState.HalfOpen);
	}

	[Fact]
	public void Closed_HasExpectedValue()
	{
		// Assert
		((int)ResilienceState.Closed).ShouldBe(0);
	}

	[Fact]
	public void Open_HasExpectedValue()
	{
		// Assert
		((int)ResilienceState.Open).ShouldBe(1);
	}

	[Fact]
	public void HalfOpen_HasExpectedValue()
	{
		// Assert
		((int)ResilienceState.HalfOpen).ShouldBe(2);
	}

	[Fact]
	public void Closed_IsDefaultValue()
	{
		// Arrange
		ResilienceState defaultState = default;

		// Assert
		defaultState.ShouldBe(ResilienceState.Closed);
	}

	[Theory]
	[InlineData(ResilienceState.Closed)]
	[InlineData(ResilienceState.Open)]
	[InlineData(ResilienceState.HalfOpen)]
	public void BeDefinedForAllValues(ResilienceState state)
	{
		// Assert
		Enum.IsDefined(state).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ResilienceState.Closed)]
	[InlineData(1, ResilienceState.Open)]
	[InlineData(2, ResilienceState.HalfOpen)]
	public void CastFromInt_ReturnsCorrectValue(int value, ResilienceState expected)
	{
		// Act
		var state = (ResilienceState)value;

		// Assert
		state.ShouldBe(expected);
	}

	[Fact]
	public void RepresentsCircuitBreakerPatternStates()
	{
		// Assert - States follow circuit breaker pattern terminology
		// Closed = normal operation
		// Open = failing fast
		// HalfOpen = testing recovery
		(ResilienceState.Closed < ResilienceState.Open).ShouldBeTrue();
		(ResilienceState.Open < ResilienceState.HalfOpen).ShouldBeTrue();
	}
}
