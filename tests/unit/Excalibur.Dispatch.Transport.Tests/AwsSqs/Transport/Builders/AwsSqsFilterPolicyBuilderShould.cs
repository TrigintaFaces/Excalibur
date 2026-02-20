// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsFilterPolicyBuilder"/> and related filter condition builders.
/// Part of S471.3 - Filter Policy builder (Sprint 471).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFilterPolicyBuilderShould : UnitTestBase
{
	#region Scope Tests

	[Fact]
	public void OnMessageAttributes_SetScopeToMessageAttributes()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.OnMessageAttributes();

		// Assert
		options.Scope.ShouldBe(AwsSqsFilterPolicyScope.MessageAttributes);
	}

	[Fact]
	public void OnMessageBody_SetScopeToMessageBody()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.OnMessageBody();

		// Assert
		options.Scope.ShouldBe(AwsSqsFilterPolicyScope.MessageBody);
	}

	[Fact]
	public void OnMessageAttributes_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		var result = builder.OnMessageAttributes();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Attribute Tests

	[Fact]
	public void Attribute_ThrowWhenAttributeNameIsNull()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute(null!));
	}

	[Fact]
	public void Attribute_ThrowWhenAttributeNameIsEmpty()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute(""));
	}

	[Fact]
	public void Attribute_ReturnAttributeBuilder()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		var attributeBuilder = builder.Attribute("priority");

		// Assert
		_ = attributeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region Equals (String) Tests

	[Fact]
	public void Equals_AddExactMatchCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").Equals("high");

		// Assert
		options.HasConditions.ShouldBeTrue();
		options.Conditions.ShouldContainKey("priority");
		var conditions = options.Conditions["priority"];
		conditions.Count.ShouldBe(1);
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.ExactMatch);
		conditions[0].Values.ShouldContain("high");
	}

	[Fact]
	public void Equals_SupportMultipleValuesWithOr()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").Equals("high").Or().Equals("urgent");

		// Assert
		var conditions = options.Conditions["priority"];
		conditions.Count.ShouldBe(2);
		conditions[0].Values.ShouldContain("high");
		conditions[1].Values.ShouldContain("urgent");
	}

	#endregion

	#region Equals (Int) Tests

	[Fact]
	public void Equals_AddNumericExactMatchCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").Equals(1);

		// Assert
		var conditions = options.Conditions["priority"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.ExactMatch);
		conditions[0].Values.ShouldContain(1);
	}

	#endregion

	#region Prefix Tests

	[Fact]
	public void Prefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute("region").Prefix(null!));
	}

	[Fact]
	public void Prefix_ThrowWhenPrefixIsEmpty()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute("region").Prefix(""));
	}

	[Fact]
	public void Prefix_AddPrefixCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("region").Prefix("us-");

		// Assert
		var conditions = options.Conditions["region"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.Prefix);
		conditions[0].Values.ShouldContain("us-");
	}

	#endregion

	#region Suffix Tests

	[Fact]
	public void Suffix_ThrowWhenSuffixIsNull()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute("email").Suffix(null!));
	}

	[Fact]
	public void Suffix_AddSuffixCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("email").Suffix("@example.com");

		// Assert
		var conditions = options.Conditions["email"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.Suffix);
		conditions[0].Values.ShouldContain("@example.com");
	}

	#endregion

	#region AnythingBut Tests

	[Fact]
	public void AnythingBut_ThrowWhenValuesIsNull()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.Attribute("status").AnythingBut(null!));
	}

	[Fact]
	public void AnythingBut_ThrowWhenValuesIsEmpty()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute("status").AnythingBut());
	}

	[Fact]
	public void AnythingBut_AddAnythingButCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("status").AnythingBut("deleted", "archived");

		// Assert
		var conditions = options.Conditions["status"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.AnythingBut);
		conditions[0].Values.ShouldContain("deleted");
		conditions[0].Values.ShouldContain("archived");
	}

	#endregion

	#region Exists Tests

	[Fact]
	public void Exists_AddExistsConditionTrue()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("metadata").Exists(true);

		// Assert
		var conditions = options.Conditions["metadata"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.Exists);
		conditions[0].Values.ShouldContain(true);
	}

	[Fact]
	public void Exists_AddExistsConditionFalse()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("deletedAt").Exists(false);

		// Assert
		var conditions = options.Conditions["deletedAt"];
		conditions[0].Values.ShouldContain(false);
	}

	[Fact]
	public void Exists_DefaultToTrue()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("metadata").Exists();

		// Assert
		var conditions = options.Conditions["metadata"];
		conditions[0].Values.ShouldContain(true);
	}

	#endregion

	#region Numeric Comparison Tests

	[Fact]
	public void GreaterThan_AddNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("amount").GreaterThan(100);

		// Assert
		var conditions = options.Conditions["amount"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.Numeric);
		conditions[0].NumericComparison.ShouldBe(">");
		conditions[0].Values.ShouldContain(100.0);
	}

	[Fact]
	public void GreaterThanOrEqual_AddNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("amount").GreaterThanOrEqual(100);

		// Assert
		var conditions = options.Conditions["amount"];
		conditions[0].NumericComparison.ShouldBe(">=");
	}

	[Fact]
	public void LessThan_AddNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").LessThan(5);

		// Assert
		var conditions = options.Conditions["priority"];
		conditions[0].NumericComparison.ShouldBe("<");
	}

	[Fact]
	public void LessThanOrEqual_AddNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").LessThanOrEqual(3);

		// Assert
		var conditions = options.Conditions["priority"];
		conditions[0].NumericComparison.ShouldBe("<=");
	}

	[Fact]
	public void Between_AddNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("score").Between(80, 100);

		// Assert
		var conditions = options.Conditions["score"];
		conditions[0].Operator.ShouldBe(AwsSqsFilterOperator.Numeric);
		conditions[0].NumericComparison.ShouldBe("between");
		conditions[0].Values.Count.ShouldBe(2);
	}

	[Fact]
	public void Between_ThrowWhenLowerGreaterThanUpper()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Attribute("score").Between(100, 80));
	}

	#endregion

	#region Fluent Chain Tests

	[Fact]
	public void And_ReturnParentBuilder()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		var parentBuilder = builder.Attribute("priority").Equals("high").And();

		// Assert
		parentBuilder.ShouldBeSameAs(builder);
	}

	[Fact]
	public void SupportMultipleAttributesWithAnd()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.Attribute("priority").Equals("high")
			   .And()
			   .Attribute("region").Prefix("us-");

		// Assert
		options.Conditions.Count.ShouldBe(2);
		options.Conditions.ShouldContainKey("priority");
		options.Conditions.ShouldContainKey("region");
	}

	[Fact]
	public void SupportComplexFilterPolicy()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		// Act
		_ = builder.OnMessageAttributes()
			   .Attribute("priority").Equals("high").Or().Equals("urgent")
			   .And()
			   .Attribute("region").Prefix("us-")
			   .And()
			   .Attribute("amount").GreaterThan(100)
			   .And()
			   .Attribute("status").AnythingBut("deleted", "archived");

		// Assert
		options.Conditions.Count.ShouldBe(4);
		options.Conditions["priority"].Count.ShouldBe(2); // "high" OR "urgent"
	}

	#endregion

	#region ToJson Tests

	[Fact]
	public void ToJson_GenerateValidJson()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		_ = builder.Attribute("priority").Equals("high");

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("priority");
		json.ShouldContain("high");
	}

	[Fact]
	public void ToJson_IncludePrefixOperator()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var builder = new AwsSqsFilterPolicyBuilder(options);

		_ = builder.Attribute("region").Prefix("us-");

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("prefix");
		json.ShouldContain("us-");
	}

	#endregion
}
