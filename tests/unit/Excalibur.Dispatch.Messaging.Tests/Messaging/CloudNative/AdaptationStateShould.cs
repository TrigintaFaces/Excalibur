// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="AdaptationState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class AdaptationStateShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AdaptationState>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(AdaptationState.Stable);
		values.ShouldContain(AdaptationState.Adapting);
		values.ShouldContain(AdaptationState.Monitoring);
	}

	[Fact]
	public void Stable_HasExpectedValue()
	{
		// Assert
		((int)AdaptationState.Stable).ShouldBe(0);
	}

	[Fact]
	public void Adapting_HasExpectedValue()
	{
		// Assert
		((int)AdaptationState.Adapting).ShouldBe(1);
	}

	[Fact]
	public void Monitoring_HasExpectedValue()
	{
		// Assert
		((int)AdaptationState.Monitoring).ShouldBe(2);
	}

	[Fact]
	public void Stable_IsDefaultValue()
	{
		// Arrange
		AdaptationState defaultState = default;

		// Assert
		defaultState.ShouldBe(AdaptationState.Stable);
	}

	[Theory]
	[InlineData(AdaptationState.Stable)]
	[InlineData(AdaptationState.Adapting)]
	[InlineData(AdaptationState.Monitoring)]
	public void BeDefinedForAllValues(AdaptationState state)
	{
		// Assert
		Enum.IsDefined(state).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, AdaptationState.Stable)]
	[InlineData(1, AdaptationState.Adapting)]
	[InlineData(2, AdaptationState.Monitoring)]
	public void CastFromInt_ReturnsCorrectValue(int value, AdaptationState expected)
	{
		// Act
		var state = (AdaptationState)value;

		// Assert
		state.ShouldBe(expected);
	}
}
