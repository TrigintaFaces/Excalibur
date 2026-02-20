// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="MessageIdStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class MessageIdStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void FromHeader_HasExpectedValue()
	{
		// Assert
		((int)MessageIdStrategy.FromHeader).ShouldBe(0);
	}

	[Fact]
	public void FromCorrelationId_HasExpectedValue()
	{
		// Assert
		((int)MessageIdStrategy.FromCorrelationId).ShouldBe(1);
	}

	[Fact]
	public void CompositeKey_HasExpectedValue()
	{
		// Assert
		((int)MessageIdStrategy.CompositeKey).ShouldBe(2);
	}

	[Fact]
	public void Custom_HasExpectedValue()
	{
		// Assert
		((int)MessageIdStrategy.Custom).ShouldBe(3);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<MessageIdStrategy>();

		// Assert
		values.ShouldContain(MessageIdStrategy.FromHeader);
		values.ShouldContain(MessageIdStrategy.FromCorrelationId);
		values.ShouldContain(MessageIdStrategy.CompositeKey);
		values.ShouldContain(MessageIdStrategy.Custom);
	}

	[Fact]
	public void HasExactlyFourValues()
	{
		// Arrange
		var values = Enum.GetValues<MessageIdStrategy>();

		// Assert
		values.Length.ShouldBe(4);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(MessageIdStrategy.FromHeader, "FromHeader")]
	[InlineData(MessageIdStrategy.FromCorrelationId, "FromCorrelationId")]
	[InlineData(MessageIdStrategy.CompositeKey, "CompositeKey")]
	[InlineData(MessageIdStrategy.Custom, "Custom")]
	public void ToString_ReturnsExpectedValue(MessageIdStrategy strategy, string expected)
	{
		// Act & Assert
		strategy.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("FromHeader", MessageIdStrategy.FromHeader)]
	[InlineData("FromCorrelationId", MessageIdStrategy.FromCorrelationId)]
	[InlineData("CompositeKey", MessageIdStrategy.CompositeKey)]
	[InlineData("Custom", MessageIdStrategy.Custom)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, MessageIdStrategy expected)
	{
		// Act
		var result = Enum.Parse<MessageIdStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("fromheader")]
	[InlineData("FROMHEADER")]
	[InlineData("custom")]
	[InlineData("CUSTOM")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<MessageIdStrategy>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<MessageIdStrategy>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsFromHeader()
	{
		// Arrange
		MessageIdStrategy strategy = default;

		// Assert
		strategy.ShouldBe(MessageIdStrategy.FromHeader);
	}

	#endregion
}
