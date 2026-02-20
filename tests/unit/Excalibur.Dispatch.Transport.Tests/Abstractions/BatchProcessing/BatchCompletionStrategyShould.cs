// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="BatchCompletionStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class BatchCompletionStrategyShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<BatchCompletionStrategy>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(BatchCompletionStrategy.Size);
		values.ShouldContain(BatchCompletionStrategy.Time);
		values.ShouldContain(BatchCompletionStrategy.SizeOrTime);
		values.ShouldContain(BatchCompletionStrategy.Dynamic);
		values.ShouldContain(BatchCompletionStrategy.ContentBased);
	}

	[Fact]
	public void Size_HasExpectedValue()
	{
		// Assert
		((int)BatchCompletionStrategy.Size).ShouldBe(0);
	}

	[Fact]
	public void Time_HasExpectedValue()
	{
		// Assert
		((int)BatchCompletionStrategy.Time).ShouldBe(1);
	}

	[Fact]
	public void SizeOrTime_HasExpectedValue()
	{
		// Assert
		((int)BatchCompletionStrategy.SizeOrTime).ShouldBe(2);
	}

	[Fact]
	public void Dynamic_HasExpectedValue()
	{
		// Assert
		((int)BatchCompletionStrategy.Dynamic).ShouldBe(3);
	}

	[Fact]
	public void ContentBased_HasExpectedValue()
	{
		// Assert
		((int)BatchCompletionStrategy.ContentBased).ShouldBe(4);
	}

	[Fact]
	public void Size_IsDefaultValue()
	{
		// Arrange
		BatchCompletionStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(BatchCompletionStrategy.Size);
	}

	[Theory]
	[InlineData(BatchCompletionStrategy.Size)]
	[InlineData(BatchCompletionStrategy.Time)]
	[InlineData(BatchCompletionStrategy.SizeOrTime)]
	[InlineData(BatchCompletionStrategy.Dynamic)]
	[InlineData(BatchCompletionStrategy.ContentBased)]
	public void BeDefinedForAllValues(BatchCompletionStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, BatchCompletionStrategy.Size)]
	[InlineData(1, BatchCompletionStrategy.Time)]
	[InlineData(2, BatchCompletionStrategy.SizeOrTime)]
	[InlineData(3, BatchCompletionStrategy.Dynamic)]
	[InlineData(4, BatchCompletionStrategy.ContentBased)]
	public void CastFromInt_ReturnsCorrectValue(int value, BatchCompletionStrategy expected)
	{
		// Act
		var strategy = (BatchCompletionStrategy)value;

		// Assert
		strategy.ShouldBe(expected);
	}

	[Fact]
	public void ContentBased_IsLastValue()
	{
		// Assert
		var maxValue = Enum.GetValues<BatchCompletionStrategy>().Max();
		maxValue.ShouldBe(BatchCompletionStrategy.ContentBased);
	}
}
