// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="BatchPriority"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class BatchPriorityShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<BatchPriority>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(BatchPriority.Low);
		values.ShouldContain(BatchPriority.Normal);
		values.ShouldContain(BatchPriority.High);
		values.ShouldContain(BatchPriority.Critical);
	}

	[Fact]
	public void Low_HasExpectedValue()
	{
		// Assert
		((int)BatchPriority.Low).ShouldBe(0);
	}

	[Fact]
	public void Normal_HasExpectedValue()
	{
		// Assert
		((int)BatchPriority.Normal).ShouldBe(1);
	}

	[Fact]
	public void High_HasExpectedValue()
	{
		// Assert
		((int)BatchPriority.High).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)BatchPriority.Critical).ShouldBe(3);
	}

	[Fact]
	public void Low_IsDefaultValue()
	{
		// Arrange
		BatchPriority defaultPriority = default;

		// Assert
		defaultPriority.ShouldBe(BatchPriority.Low);
	}

	[Theory]
	[InlineData(BatchPriority.Low)]
	[InlineData(BatchPriority.Normal)]
	[InlineData(BatchPriority.High)]
	[InlineData(BatchPriority.Critical)]
	public void BeDefinedForAllValues(BatchPriority priority)
	{
		// Assert
		Enum.IsDefined(priority).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, BatchPriority.Low)]
	[InlineData(1, BatchPriority.Normal)]
	[InlineData(2, BatchPriority.High)]
	[InlineData(3, BatchPriority.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, BatchPriority expected)
	{
		// Act
		var priority = (BatchPriority)value;

		// Assert
		priority.ShouldBe(expected);
	}

	[Fact]
	public void HaveCorrectPriorityOrder()
	{
		// Assert - values should increase with priority
		((int)BatchPriority.Low).ShouldBeLessThan((int)BatchPriority.Normal);
		((int)BatchPriority.Normal).ShouldBeLessThan((int)BatchPriority.High);
		((int)BatchPriority.High).ShouldBeLessThan((int)BatchPriority.Critical);
	}

	[Fact]
	public void Critical_IsHighestPriority()
	{
		// Assert
		var maxValue = Enum.GetValues<BatchPriority>().Max();
		maxValue.ShouldBe(BatchPriority.Critical);
	}
}
