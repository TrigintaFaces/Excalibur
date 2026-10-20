// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="FilterParser"/> to verify filter key parsing behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
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

	/// <summary>
	/// An unrecognised suffix is refused rather than read as equality.
	/// </summary>
	/// <remarks>
	/// This arm used to assert the opposite, and that is what made the defect findable: a mistyped
	/// suffix -- "Amount:gtt" for "Amount:gt" -- silently became an EQUALITY filter on Amount, so the
	/// query ran, returned a wrong set, and reported nothing. The message has to name the suffix and the
	/// valid ones, because the author of a typo cannot see it by re-reading the key.
	/// </remarks>
	[Fact]
	public void Parse_UnknownOperator_Throws()
	{
		var error = Should.Throw<ArgumentException>(() => FilterParser.Parse("Field:gtt"));

		error.Message.ShouldContain("gtt", Case.Sensitive);
		error.Message.ShouldContain("gte", Case.Sensitive);
	}

	/// <summary>
	/// CONTROL: every documented suffix is still accepted, including the explicit "eq" that the old
	/// fallback accepted only by accident.
	/// </summary>
	[Theory]
	[InlineData("Field:eq", FilterOperator.Equals)]
	[InlineData("Field:EQ", FilterOperator.Equals)]
	[InlineData("Field:neq", FilterOperator.NotEquals)]
	[InlineData("Field:gt", FilterOperator.GreaterThan)]
	[InlineData("Field:gte", FilterOperator.GreaterThanOrEqual)]
	[InlineData("Field:lt", FilterOperator.LessThan)]
	[InlineData("Field:lte", FilterOperator.LessThanOrEqual)]
	[InlineData("Field:in", FilterOperator.In)]
	[InlineData("Field:contains", FilterOperator.Contains)]
	public void Parse_EveryDocumentedSuffix_IsAccepted(string key, FilterOperator expected) =>
		FilterParser.Parse(key).Operator.ShouldBe(expected);

	[Fact]
	public void Parse_NullKey_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => FilterParser.Parse(null!));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(":gt")]
	public void Parse_KeyWithNoPropertyName_Throws(string key) =>
		Should.Throw<ArgumentException>(() => FilterParser.Parse(key));

	/// <summary>
	/// The first colon separates, so everything after it must be one suffix. "Field:Name:gt" is not a
	/// property path -- paths use dots -- so it is a malformed key, not an equality filter on "Field".
	/// </summary>
	[Fact]
	public void Parse_KeyWithMultipleColons_Throws() =>
		Should.Throw<ArgumentException>(() => FilterParser.Parse("Field:Name:gt"));

	/// <summary>
	/// A trailing colon is a suffix the author started and did not finish.
	/// </summary>
	[Fact]
	public void Parse_KeyEndingWithColon_Throws() =>
		Should.Throw<ArgumentException>(() => FilterParser.Parse("Status:"));
}
