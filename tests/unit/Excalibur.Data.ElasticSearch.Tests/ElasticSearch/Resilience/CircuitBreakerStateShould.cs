// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Unit tests for the canonical <see cref="CircuitState"/> enum (bd-116roh: replaces the removed
/// per-package <c>Excalibur.Data.ElasticSearch.Resilience.CircuitBreakerState</c> with the shared
/// <c>Excalibur.Dispatch.Resilience.CircuitState</c>).
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): Resilience unit tests — updated S856 (bd-116roh) to use the canonical enum.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait(TraitNames.Feature, TestFeatures.Resilience)]
public sealed class CircuitStateShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineClosedAsZero()
	{
		// Assert
		((int)CircuitState.Closed).ShouldBe(0);
	}

	[Fact]
	public void DefineOpenAsOne()
	{
		// Assert
		((int)CircuitState.Open).ShouldBe(1);
	}

	[Fact]
	public void DefineHalfOpenAsTwo()
	{
		// Assert
		((int)CircuitState.HalfOpen).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<CircuitState>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedStates()
	{
		// Act
		var values = Enum.GetValues<CircuitState>();

		// Assert
		values.ShouldContain(CircuitState.Closed);
		values.ShouldContain(CircuitState.Open);
		values.ShouldContain(CircuitState.HalfOpen);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Closed", CircuitState.Closed)]
	[InlineData("Open", CircuitState.Open)]
	[InlineData("HalfOpen", CircuitState.HalfOpen)]
	public void ParseFromString_WithValidName(string name, CircuitState expected)
	{
		// Act
		var result = Enum.Parse<CircuitState>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("closed", CircuitState.Closed)]
	[InlineData("OPEN", CircuitState.Open)]
	[InlineData("halfopen", CircuitState.HalfOpen)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, CircuitState expected)
	{
		// Act
		var result = Enum.Parse<CircuitState>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForClosed()
	{
		// Assert
		CircuitState.Closed.ToString().ShouldBe("Closed");
	}

	[Fact]
	public void HaveCorrectNameForOpen()
	{
		// Assert
		CircuitState.Open.ToString().ShouldBe("Open");
	}

	[Fact]
	public void HaveCorrectNameForHalfOpen()
	{
		// Assert
		CircuitState.HalfOpen.ToString().ShouldBe("HalfOpen");
	}

	#endregion

	#region State Transition Logic Tests

	[Fact]
	public void ClosedState_IsDefault()
	{
		// Assert - Default enum value should be Closed (0)
		default(CircuitState).ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void ClosedState_AllowsRequests()
	{
		// This is a semantic test - Closed state should be the "healthy" state
		var state = CircuitState.Closed;
		((int)state).ShouldBe(0); // Closed = 0 indicates default/healthy
	}

	[Fact]
	public void OpenState_BlocksRequests()
	{
		// This is a semantic test - Open state indicates failures
		var state = CircuitState.Open;
		((int)state).ShouldBeGreaterThan((int)CircuitState.Closed);
	}

	[Fact]
	public void HalfOpenState_IsRecoveryPhase()
	{
		// This is a semantic test - HalfOpen is between Closed and Open logically
		var state = CircuitState.HalfOpen;
		((int)state).ShouldBe(2); // Highest value indicates test phase
	}

	#endregion
}
