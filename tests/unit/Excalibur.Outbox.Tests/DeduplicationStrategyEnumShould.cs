// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicationStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeduplicationStrategyEnumShould : UnitTestBase
{
	#region Value Tests

	[Fact]
	public void MessageId_HasValueZero()
	{
		// Assert
		((int)DeduplicationStrategy.MessageId).ShouldBe(0);
	}

	[Fact]
	public void ContentHash_HasValueOne()
	{
		// Assert
		((int)DeduplicationStrategy.ContentHash).ShouldBe(1);
	}

	[Fact]
	public void Composite_HasValueTwo()
	{
		// Assert
		((int)DeduplicationStrategy.Composite).ShouldBe(2);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsMessageId()
	{
		// Arrange & Act
		var strategy = default(DeduplicationStrategy);

		// Assert
		strategy.ShouldBe(DeduplicationStrategy.MessageId);
	}

	#endregion

	#region Enum Member Tests

	[Fact]
	public void Enum_HasExactlyThreeMembers()
	{
		// Arrange & Act
		var values = Enum.GetValues<DeduplicationStrategy>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void AllStrategies_HaveUniqueValues()
	{
		// Arrange
		var values = Enum.GetValues<DeduplicationStrategy>();

		// Act
		var distinctCount = values.Distinct().Count();

		// Assert
		distinctCount.ShouldBe(values.Length);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("MessageId", DeduplicationStrategy.MessageId)]
	[InlineData("ContentHash", DeduplicationStrategy.ContentHash)]
	[InlineData("Composite", DeduplicationStrategy.Composite)]
	public void Parse_ValidString_ReturnsCorrectValue(string value, DeduplicationStrategy expected)
	{
		// Act
		var result = Enum.Parse<DeduplicationStrategy>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(0, DeduplicationStrategy.MessageId)]
	[InlineData(1, DeduplicationStrategy.ContentHash)]
	[InlineData(2, DeduplicationStrategy.Composite)]
	public void CastFromInt_ReturnsCorrectValue(int value, DeduplicationStrategy expected)
	{
		// Act
		var result = (DeduplicationStrategy)value;

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region IsDefined Tests

	[Theory]
	[InlineData(DeduplicationStrategy.MessageId)]
	[InlineData(DeduplicationStrategy.ContentHash)]
	[InlineData(DeduplicationStrategy.Composite)]
	public void IsDefined_ForAllValidValues_ReturnsTrue(DeduplicationStrategy strategy)
	{
		// Act & Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Fact]
	public void IsDefined_ForInvalidValue_ReturnsFalse()
	{
		// Arrange
		var invalidStrategy = (DeduplicationStrategy)999;

		// Act & Assert
		Enum.IsDefined(invalidStrategy).ShouldBeFalse();
	}

	#endregion
}
