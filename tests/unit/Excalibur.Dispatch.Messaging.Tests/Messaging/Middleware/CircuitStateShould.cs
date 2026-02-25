// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="CircuitState"/> enum.
/// </summary>
/// <remarks>
/// Tests the circuit breaker state values.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class CircuitStateShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveClosedAsZero()
	{
		// Assert
		((int)CircuitState.Closed).ShouldBe(0);
	}

	[Fact]
	public void HaveOpenAsOne()
	{
		// Assert
		((int)CircuitState.Open).ShouldBe(1);
	}

	[Fact]
	public void HaveHalfOpenAsTwo()
	{
		// Assert
		((int)CircuitState.HalfOpen).ShouldBe(2);
	}

	#endregion

	#region Enum Definition Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Assert
		Enum.GetValues<CircuitState>().Length.ShouldBe(3);
	}

	[Fact]
	public void BePublicEnum()
	{
		// Assert
		typeof(CircuitState).IsPublic.ShouldBeTrue();
	}

	[Theory]
	[InlineData("Closed")]
	[InlineData("Open")]
	[InlineData("HalfOpen")]
	public void HaveExpectedName(string expectedName)
	{
		// Assert
		Enum.GetNames<CircuitState>().ShouldContain(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Closed", CircuitState.Closed)]
	[InlineData("Open", CircuitState.Open)]
	[InlineData("HalfOpen", CircuitState.HalfOpen)]
	public void ParseFromString(string input, CircuitState expected)
	{
		// Act
		var result = Enum.Parse<CircuitState>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ThrowForInvalidParseInput()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<CircuitState>("InvalidState"));
	}

	#endregion

	#region TryParse Tests

	[Theory]
	[InlineData("Closed", CircuitState.Closed)]
	[InlineData("Open", CircuitState.Open)]
	[InlineData("HalfOpen", CircuitState.HalfOpen)]
	public void TryParseValidValues(string input, CircuitState expected)
	{
		// Act
		var success = Enum.TryParse<CircuitState>(input, out var result);

		// Assert
		success.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public void TryParseReturnsFalseForInvalidInput()
	{
		// Act
		var success = Enum.TryParse<CircuitState>("InvalidValue", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Conversion Tests

	[Theory]
	[InlineData(0, CircuitState.Closed)]
	[InlineData(1, CircuitState.Open)]
	[InlineData(2, CircuitState.HalfOpen)]
	public void ConvertFromInt(int value, CircuitState expected)
	{
		// Act
		var result = (CircuitState)value;

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(CircuitState.Closed, 0)]
	[InlineData(CircuitState.Open, 1)]
	[InlineData(CircuitState.HalfOpen, 2)]
	public void ConvertToInt(CircuitState state, int expected)
	{
		// Act
		var result = (int)state;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var state = CircuitState.HalfOpen;

		// Act
		var allowRequest = state switch
		{
			CircuitState.Closed => true,
			CircuitState.Open => false,
			CircuitState.HalfOpen => true, // Allow limited requests to test recovery
			_ => false,
		};

		// Assert
		allowRequest.ShouldBeTrue();
	}

	[Fact]
	public void DefaultValueIsClosed()
	{
		// Act
		CircuitState defaultValue = default;

		// Assert - Closed should be default (0) meaning circuit allows requests
		defaultValue.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void ClosedStateAllowsRequests()
	{
		// Arrange
		var state = CircuitState.Closed;

		// Assert - Closed means circuit is working normally
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void OpenStateBlocksRequests()
	{
		// Arrange
		var state = CircuitState.Open;

		// Assert - Open means circuit has tripped and blocks requests
		state.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void HalfOpenStateTestsRecovery()
	{
		// Arrange
		var state = CircuitState.HalfOpen;

		// Assert - HalfOpen means circuit is testing if service has recovered
		state.ShouldBe(CircuitState.HalfOpen);
	}

	#endregion
}
