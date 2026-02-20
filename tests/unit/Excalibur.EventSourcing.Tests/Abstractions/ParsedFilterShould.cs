// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="ParsedFilter"/> record to verify immutability and property behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ParsedFilterShould
{
	[Fact]
	public void Create_WithPropertyNameAndOperator()
	{
		// Arrange & Act
		var filter = new ParsedFilter("Status", FilterOperator.Equals);

		// Assert
		filter.PropertyName.ShouldBe("Status");
		filter.Operator.ShouldBe(FilterOperator.Equals);
	}

	[Theory]
	[InlineData(FilterOperator.Equals)]
	[InlineData(FilterOperator.NotEquals)]
	[InlineData(FilterOperator.GreaterThan)]
	[InlineData(FilterOperator.GreaterThanOrEqual)]
	[InlineData(FilterOperator.LessThan)]
	[InlineData(FilterOperator.LessThanOrEqual)]
	[InlineData(FilterOperator.In)]
	[InlineData(FilterOperator.Contains)]
	public void SupportAllFilterOperators(FilterOperator op)
	{
		// Arrange & Act
		var filter = new ParsedFilter("Field", op);

		// Assert
		filter.Operator.ShouldBe(op);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var filter1 = new ParsedFilter("Amount", FilterOperator.GreaterThan);
		var filter2 = new ParsedFilter("Amount", FilterOperator.GreaterThan);

		// Assert
		filter1.ShouldBe(filter2);
	}

	[Fact]
	public void SupportRecordInequality_WhenPropertyNameDiffers()
	{
		// Arrange
		var filter1 = new ParsedFilter("Amount", FilterOperator.GreaterThan);
		var filter2 = new ParsedFilter("Price", FilterOperator.GreaterThan);

		// Assert
		filter1.ShouldNotBe(filter2);
	}

	[Fact]
	public void SupportRecordInequality_WhenOperatorDiffers()
	{
		// Arrange
		var filter1 = new ParsedFilter("Amount", FilterOperator.GreaterThan);
		var filter2 = new ParsedFilter("Amount", FilterOperator.LessThan);

		// Assert
		filter1.ShouldNotBe(filter2);
	}

	[Fact]
	public void SupportWithExpression_ForImmutableUpdates()
	{
		// Arrange
		var original = new ParsedFilter("Status", FilterOperator.Equals);

		// Act
		var updated = original with { Operator = FilterOperator.NotEquals };

		// Assert
		updated.Operator.ShouldBe(FilterOperator.NotEquals);
		original.Operator.ShouldBe(FilterOperator.Equals);
	}
}
