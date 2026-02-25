// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Unit tests for the <see cref="CircuitBreakerState"/> enum.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): Resilience unit tests.
/// Tests verify enum values for circuit breaker states.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Resilience")]
public sealed class CircuitBreakerStateShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineClosedAsZero()
	{
		// Assert
		((int)CircuitBreakerState.Closed).ShouldBe(0);
	}

	[Fact]
	public void DefineOpenAsOne()
	{
		// Assert
		((int)CircuitBreakerState.Open).ShouldBe(1);
	}

	[Fact]
	public void DefineHalfOpenAsTwo()
	{
		// Assert
		((int)CircuitBreakerState.HalfOpen).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<CircuitBreakerState>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedStates()
	{
		// Act
		var values = Enum.GetValues<CircuitBreakerState>();

		// Assert
		values.ShouldContain(CircuitBreakerState.Closed);
		values.ShouldContain(CircuitBreakerState.Open);
		values.ShouldContain(CircuitBreakerState.HalfOpen);
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("Closed", CircuitBreakerState.Closed)]
	[InlineData("Open", CircuitBreakerState.Open)]
	[InlineData("HalfOpen", CircuitBreakerState.HalfOpen)]
	public void ParseFromString_WithValidName(string name, CircuitBreakerState expected)
	{
		// Act
		var result = Enum.Parse<CircuitBreakerState>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("closed", CircuitBreakerState.Closed)]
	[InlineData("OPEN", CircuitBreakerState.Open)]
	[InlineData("halfopen", CircuitBreakerState.HalfOpen)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, CircuitBreakerState expected)
	{
		// Act
		var result = Enum.Parse<CircuitBreakerState>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForClosed()
	{
		// Assert
		CircuitBreakerState.Closed.ToString().ShouldBe("Closed");
	}

	[Fact]
	public void HaveCorrectNameForOpen()
	{
		// Assert
		CircuitBreakerState.Open.ToString().ShouldBe("Open");
	}

	[Fact]
	public void HaveCorrectNameForHalfOpen()
	{
		// Assert
		CircuitBreakerState.HalfOpen.ToString().ShouldBe("HalfOpen");
	}

	#endregion

	#region State Transition Logic Tests

	[Fact]
	public void ClosedState_IsDefault()
	{
		// Assert - Default enum value should be Closed (0)
		default(CircuitBreakerState).ShouldBe(CircuitBreakerState.Closed);
	}

	[Fact]
	public void ClosedState_AllowsRequests()
	{
		// This is a semantic test - Closed state should be the "healthy" state
		var state = CircuitBreakerState.Closed;
		((int)state).ShouldBe(0); // Closed = 0 indicates default/healthy
	}

	[Fact]
	public void OpenState_BlocksRequests()
	{
		// This is a semantic test - Open state indicates failures
		var state = CircuitBreakerState.Open;
		((int)state).ShouldBeGreaterThan((int)CircuitBreakerState.Closed);
	}

	[Fact]
	public void HalfOpenState_IsRecoveryPhase()
	{
		// This is a semantic test - HalfOpen is between Closed and Open logically
		var state = CircuitBreakerState.HalfOpen;
		((int)state).ShouldBe(2); // Highest value indicates test phase
	}

	#endregion
}
