// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicationStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class DeduplicationStrategyShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DeduplicationStrategy>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(DeduplicationStrategy.MessageId);
		values.ShouldContain(DeduplicationStrategy.ContentHash);
		values.ShouldContain(DeduplicationStrategy.Composite);
	}

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

	[Fact]
	public void MessageId_IsDefaultValue()
	{
		// Arrange
		DeduplicationStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(DeduplicationStrategy.MessageId);
	}

	[Theory]
	[InlineData(DeduplicationStrategy.MessageId)]
	[InlineData(DeduplicationStrategy.ContentHash)]
	[InlineData(DeduplicationStrategy.Composite)]
	public void BeDefinedForAllValues(DeduplicationStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, DeduplicationStrategy.MessageId)]
	[InlineData(1, DeduplicationStrategy.ContentHash)]
	[InlineData(2, DeduplicationStrategy.Composite)]
	public void CastFromInt_ReturnsCorrectValue(int value, DeduplicationStrategy expected)
	{
		// Act
		var strategy = (DeduplicationStrategy)value;

		// Assert
		strategy.ShouldBe(expected);
	}
}
