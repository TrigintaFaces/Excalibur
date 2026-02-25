// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="FilterParser"/> to verify filter key parsing behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FilterParserShould
{
	[Fact]
	public void Parse_SimpleKey_ReturnsEqualsOperator()
	{
		// Arrange & Act
		var result = FilterParser.Parse("Status");

		// Assert
		result.PropertyName.ShouldBe("Status");
		result.Operator.ShouldBe(FilterOperator.Equals);
	}

	[Theory]
	[InlineData("Amount:gt", "Amount", FilterOperator.GreaterThan)]
	[InlineData("Amount:gte", "Amount", FilterOperator.GreaterThanOrEqual)]
	[InlineData("Amount:lt", "Amount", FilterOperator.LessThan)]
	[InlineData("Amount:lte", "Amount", FilterOperator.LessThanOrEqual)]
	[InlineData("Status:neq", "Status", FilterOperator.NotEquals)]
	[InlineData("Tags:in", "Tags", FilterOperator.In)]
	[InlineData("Name:contains", "Name", FilterOperator.Contains)]
	public void Parse_KeyWithOperator_ReturnsCorrectOperator(string key, string expectedProperty, FilterOperator expectedOperator)
	{
		// Arrange & Act
		var result = FilterParser.Parse(key);

		// Assert
		result.PropertyName.ShouldBe(expectedProperty);
		result.Operator.ShouldBe(expectedOperator);
	}

	[Theory]
	[InlineData("Amount:GT")]
	[InlineData("Amount:Gt")]
	[InlineData("Amount:gT")]
	public void Parse_OperatorIsCaseInsensitive(string key)
	{
		// Arrange & Act
		var result = FilterParser.Parse(key);

		// Assert
		result.Operator.ShouldBe(FilterOperator.GreaterThan);
	}

	[Fact]
	public void Parse_UnknownOperator_DefaultsToEquals()
	{
		// Arrange & Act
		var result = FilterParser.Parse("Field:unknown");

		// Assert
		result.PropertyName.ShouldBe("Field");
		result.Operator.ShouldBe(FilterOperator.Equals);
	}

	[Fact]
	public void Parse_NullKey_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => FilterParser.Parse(null!));
	}

	[Fact]
	public void Parse_EmptyKey_ReturnsEmptyPropertyName()
	{
		// Arrange & Act
		var result = FilterParser.Parse(string.Empty);

		// Assert
		result.PropertyName.ShouldBe(string.Empty);
		result.Operator.ShouldBe(FilterOperator.Equals);
	}

	[Fact]
	public void Parse_KeyWithMultipleColons_UsesFirstColonAsSeparator()
	{
		// Arrange & Act
		var result = FilterParser.Parse("Field:Name:gt");

		// Assert
		result.PropertyName.ShouldBe("Field");
		// "Name:gt" is not a valid operator, so defaults to Equals
		result.Operator.ShouldBe(FilterOperator.Equals);
	}

	[Fact]
	public void Parse_KeyEndingWithColon_HasEmptyOperator()
	{
		// Arrange & Act
		var result = FilterParser.Parse("Status:");

		// Assert
		result.PropertyName.ShouldBe("Status");
		// Empty string is not a known operator, defaults to Equals
		result.Operator.ShouldBe(FilterOperator.Equals);
	}
}
