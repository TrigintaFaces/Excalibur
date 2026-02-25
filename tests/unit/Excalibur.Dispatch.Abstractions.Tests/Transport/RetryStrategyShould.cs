// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="RetryStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class RetryStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void FixedDelay_HasExpectedValue()
	{
		// Assert
		((int)RetryStrategy.FixedDelay).ShouldBe(0);
	}

	[Fact]
	public void ExponentialBackoff_HasExpectedValue()
	{
		// Assert
		((int)RetryStrategy.ExponentialBackoff).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<RetryStrategy>();

		// Assert
		values.ShouldContain(RetryStrategy.FixedDelay);
		values.ShouldContain(RetryStrategy.ExponentialBackoff);
	}

	[Fact]
	public void HasExactlyTwoValues()
	{
		// Arrange
		var values = Enum.GetValues<RetryStrategy>();

		// Assert
		values.Length.ShouldBe(2);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(RetryStrategy.FixedDelay, "FixedDelay")]
	[InlineData(RetryStrategy.ExponentialBackoff, "ExponentialBackoff")]
	public void ToString_ReturnsExpectedValue(RetryStrategy strategy, string expected)
	{
		// Act & Assert
		strategy.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("FixedDelay", RetryStrategy.FixedDelay)]
	[InlineData("ExponentialBackoff", RetryStrategy.ExponentialBackoff)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, RetryStrategy expected)
	{
		// Act
		var result = Enum.Parse<RetryStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsFixedDelay()
	{
		// Arrange
		RetryStrategy strategy = default;

		// Assert
		strategy.ShouldBe(RetryStrategy.FixedDelay);
	}

	#endregion
}
