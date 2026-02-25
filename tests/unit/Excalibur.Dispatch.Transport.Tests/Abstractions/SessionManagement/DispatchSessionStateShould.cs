// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="DispatchSessionState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class DispatchSessionStateShould
{
	[Fact]
	public void HaveSixDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchSessionState>();

		// Assert
		values.Length.ShouldBe(6);
		values.ShouldContain(DispatchSessionState.Active);
		values.ShouldContain(DispatchSessionState.Idle);
		values.ShouldContain(DispatchSessionState.Locked);
		values.ShouldContain(DispatchSessionState.Expired);
		values.ShouldContain(DispatchSessionState.Closing);
		values.ShouldContain(DispatchSessionState.Closed);
	}

	[Fact]
	public void Active_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Active).ShouldBe(0);
	}

	[Fact]
	public void Idle_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Idle).ShouldBe(1);
	}

	[Fact]
	public void Locked_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Locked).ShouldBe(2);
	}

	[Fact]
	public void Expired_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Expired).ShouldBe(3);
	}

	[Fact]
	public void Closing_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Closing).ShouldBe(4);
	}

	[Fact]
	public void Closed_HasExpectedValue()
	{
		// Assert
		((int)DispatchSessionState.Closed).ShouldBe(5);
	}

	[Fact]
	public void Active_IsDefaultValue()
	{
		// Arrange
		DispatchSessionState defaultState = default;

		// Assert
		defaultState.ShouldBe(DispatchSessionState.Active);
	}

	[Theory]
	[InlineData(DispatchSessionState.Active)]
	[InlineData(DispatchSessionState.Idle)]
	[InlineData(DispatchSessionState.Locked)]
	[InlineData(DispatchSessionState.Expired)]
	[InlineData(DispatchSessionState.Closing)]
	[InlineData(DispatchSessionState.Closed)]
	public void BeDefinedForAllValues(DispatchSessionState state)
	{
		// Assert
		Enum.IsDefined(state).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, DispatchSessionState.Active)]
	[InlineData(1, DispatchSessionState.Idle)]
	[InlineData(2, DispatchSessionState.Locked)]
	[InlineData(3, DispatchSessionState.Expired)]
	[InlineData(4, DispatchSessionState.Closing)]
	[InlineData(5, DispatchSessionState.Closed)]
	public void CastFromInt_ReturnsCorrectValue(int value, DispatchSessionState expected)
	{
		// Act
		var state = (DispatchSessionState)value;

		// Assert
		state.ShouldBe(expected);
	}

	[Fact]
	public void HaveLifecycleStatesOrderedByProgression()
	{
		// Assert - States progress from active through closing to closed
		(DispatchSessionState.Active < DispatchSessionState.Idle).ShouldBeTrue();
		(DispatchSessionState.Closing < DispatchSessionState.Closed).ShouldBeTrue();
	}
}
