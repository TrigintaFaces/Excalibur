// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="FailureHandlingStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class FailureHandlingStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void FailFast_HasExpectedValue()
	{
		// Assert
		((int)FailureHandlingStrategy.FailFast).ShouldBe(0);
	}

	[Fact]
	public void RetryThenFail_HasExpectedValue()
	{
		// Assert
		((int)FailureHandlingStrategy.RetryThenFail).ShouldBe(1);
	}

	[Fact]
	public void RetryIndefinitely_HasExpectedValue()
	{
		// Assert
		((int)FailureHandlingStrategy.RetryIndefinitely).ShouldBe(2);
	}

	[Fact]
	public void CreateOnDemand_HasExpectedValue()
	{
		// Assert
		((int)FailureHandlingStrategy.CreateOnDemand).ShouldBe(3);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<FailureHandlingStrategy>();

		// Assert
		values.ShouldContain(FailureHandlingStrategy.FailFast);
		values.ShouldContain(FailureHandlingStrategy.RetryThenFail);
		values.ShouldContain(FailureHandlingStrategy.RetryIndefinitely);
		values.ShouldContain(FailureHandlingStrategy.CreateOnDemand);
	}

	[Fact]
	public void HasExactlyFourValues()
	{
		// Arrange
		var values = Enum.GetValues<FailureHandlingStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(FailureHandlingStrategy.FailFast, "FailFast")]
	[InlineData(FailureHandlingStrategy.RetryThenFail, "RetryThenFail")]
	[InlineData(FailureHandlingStrategy.RetryIndefinitely, "RetryIndefinitely")]
	[InlineData(FailureHandlingStrategy.CreateOnDemand, "CreateOnDemand")]
	public void ToString_ReturnsExpectedValue(FailureHandlingStrategy strategy, string expected)
	{
		// Act & Assert
		strategy.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("FailFast", FailureHandlingStrategy.FailFast)]
	[InlineData("RetryThenFail", FailureHandlingStrategy.RetryThenFail)]
	[InlineData("RetryIndefinitely", FailureHandlingStrategy.RetryIndefinitely)]
	[InlineData("CreateOnDemand", FailureHandlingStrategy.CreateOnDemand)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, FailureHandlingStrategy expected)
	{
		// Act
		var result = Enum.Parse<FailureHandlingStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("failfast")]
	[InlineData("FAILFAST")]
	[InlineData("retrythenfail")]
	[InlineData("RETRYTHENFAIL")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<FailureHandlingStrategy>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<FailureHandlingStrategy>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsFailFast()
	{
		// Arrange
		FailureHandlingStrategy strategy = default;

		// Assert
		strategy.ShouldBe(FailureHandlingStrategy.FailFast);
	}

	#endregion
}
