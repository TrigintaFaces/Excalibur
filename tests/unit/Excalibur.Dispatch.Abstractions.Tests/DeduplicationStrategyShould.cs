// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicationStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Deduplication")]
[Trait("Priority", "0")]
public sealed class DeduplicationStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void MessageId_HasExpectedValue()
	{
		// Assert
		((int)DeduplicationStrategy.MessageId).ShouldBe(0);
	}

	[Fact]
	public void ContentHash_HasExpectedValue()
	{
		// Assert
		((int)DeduplicationStrategy.ContentHash).ShouldBe(1);
	}

	[Fact]
	public void Composite_HasExpectedValue()
	{
		// Assert
		((int)DeduplicationStrategy.Composite).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<DeduplicationStrategy>();

		// Assert
		values.ShouldContain(DeduplicationStrategy.MessageId);
		values.ShouldContain(DeduplicationStrategy.ContentHash);
		values.ShouldContain(DeduplicationStrategy.Composite);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<DeduplicationStrategy>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(DeduplicationStrategy.MessageId, "MessageId")]
	[InlineData(DeduplicationStrategy.ContentHash, "ContentHash")]
	[InlineData(DeduplicationStrategy.Composite, "Composite")]
	public void ToString_ReturnsExpectedValue(DeduplicationStrategy strategy, string expected)
	{
		// Act & Assert
		strategy.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("MessageId", DeduplicationStrategy.MessageId)]
	[InlineData("ContentHash", DeduplicationStrategy.ContentHash)]
	[InlineData("Composite", DeduplicationStrategy.Composite)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, DeduplicationStrategy expected)
	{
		// Act
		var result = Enum.Parse<DeduplicationStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("messageid")]
	[InlineData("MESSAGEID")]
	[InlineData("contenthash")]
	[InlineData("CONTENTHASH")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<DeduplicationStrategy>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<DeduplicationStrategy>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsMessageId()
	{
		// Arrange
		DeduplicationStrategy strategy = default;

		// Assert
		strategy.ShouldBe(DeduplicationStrategy.MessageId);
	}

	#endregion
}
