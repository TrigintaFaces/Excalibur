// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;

namespace Excalibur.Tests.A3.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthenticationStateShould : UnitTestBase
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AuthenticationState>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(AuthenticationState.Anonymous);
		values.ShouldContain(AuthenticationState.Authenticated);
		values.ShouldContain(AuthenticationState.Identified);
	}

	[Fact]
	public void Anonymous_HasExpectedValue()
	{
		// Assert
		((int)AuthenticationState.Anonymous).ShouldBe(0);
	}

	[Fact]
	public void Authenticated_HasExpectedValue()
	{
		// Assert
		((int)AuthenticationState.Authenticated).ShouldBe(1);
	}

	[Fact]
	public void Identified_HasExpectedValue()
	{
		// Assert
		((int)AuthenticationState.Identified).ShouldBe(2);
	}

	[Fact]
	public void Anonymous_IsDefaultValue()
	{
		// Arrange
		AuthenticationState defaultState = default;

		// Assert
		defaultState.ShouldBe(AuthenticationState.Anonymous);
	}

	[Theory]
	[InlineData(AuthenticationState.Anonymous)]
	[InlineData(AuthenticationState.Authenticated)]
	[InlineData(AuthenticationState.Identified)]
	public void BeDefinedForAllValues(AuthenticationState state)
	{
		// Assert
		Enum.IsDefined(state).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, AuthenticationState.Anonymous)]
	[InlineData(1, AuthenticationState.Authenticated)]
	[InlineData(2, AuthenticationState.Identified)]
	public void CastFromInt_ReturnsCorrectValue(int value, AuthenticationState expected)
	{
		// Act
		var state = (AuthenticationState)value;

		// Assert
		state.ShouldBe(expected);
	}
}
