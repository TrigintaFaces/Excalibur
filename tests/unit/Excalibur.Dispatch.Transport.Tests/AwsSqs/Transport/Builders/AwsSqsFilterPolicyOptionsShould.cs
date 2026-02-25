// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsFilterPolicyOptions"/> and related classes.
/// Part of S471.4 - Unit Tests for SNS Builders (Sprint 471).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFilterPolicyOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void Constructor_HaveMessageAttributesScope()
	{
		// Arrange & Act
		var options = new AwsSqsFilterPolicyOptions();

		// Assert
		options.Scope.ShouldBe(AwsSqsFilterPolicyScope.MessageAttributes);
	}

	[Fact]
	public void Constructor_HaveEmptyConditions()
	{
		// Arrange & Act
		var options = new AwsSqsFilterPolicyOptions();

		// Assert
		options.Conditions.ShouldBeEmpty();
		options.HasConditions.ShouldBeFalse();
	}

	#endregion

	#region Scope Tests

	[Fact]
	public void Scope_CanBeSetToMessageBody()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();

		// Act
		options.Scope = AwsSqsFilterPolicyScope.MessageBody;

		// Assert
		options.Scope.ShouldBe(AwsSqsFilterPolicyScope.MessageBody);
	}

	#endregion

	#region Conditions Tests

	[Fact]
	public void Conditions_CanAddCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.ExactMatch,
			Values = new List<object> { "high" },
		};

		// Act
		options.Conditions["priority"] = new List<AwsSqsFilterCondition> { condition };

		// Assert
		options.HasConditions.ShouldBeTrue();
		options.Conditions.ShouldContainKey("priority");
	}

	[Fact]
	public void Conditions_SupportMultipleAttributes()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();

		// Act
		options.Conditions["priority"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.ExactMatch, Values = new List<object> { "high" } },
		};
		options.Conditions["region"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.Prefix, Values = new List<object> { "us-" } },
		};

		// Assert
		options.Conditions.Count.ShouldBe(2);
	}

	#endregion

	#region ToJson Tests

	[Fact]
	public void ToJson_ReturnEmptyObjectForNoConditions()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldBe("{}");
	}

	[Fact]
	public void ToJson_SerializeExactMatchCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["status"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.ExactMatch, Values = new List<object> { "active" } },
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"status\"");
		json.ShouldContain("active");
	}

	[Fact]
	public void ToJson_SerializePrefixCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["region"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.Prefix, Values = new List<object> { "us-" } },
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"prefix\"");
		json.ShouldContain("us-");
	}

	[Fact]
	public void ToJson_SerializeSuffixCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["email"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.Suffix, Values = new List<object> { "@example.com" } },
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"suffix\"");
		json.ShouldContain("@example.com");
	}

	[Fact]
	public void ToJson_SerializeAnythingButCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["status"] = new List<AwsSqsFilterCondition>
		{
			new()
			{
				Operator = AwsSqsFilterOperator.AnythingBut,
				Values = new List<object> { "deleted", "archived" },
			},
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"anything-but\"");
		json.ShouldContain("deleted");
		json.ShouldContain("archived");
	}

	[Fact]
	public void ToJson_SerializeExistsCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["metadata"] = new List<AwsSqsFilterCondition>
		{
			new() { Operator = AwsSqsFilterOperator.Exists, Values = new List<object> { true } },
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"exists\"");
		json.ShouldContain("true");
	}

	[Fact]
	public void ToJson_SerializeNumericCondition()
	{
		// Arrange
		var options = new AwsSqsFilterPolicyOptions();
		options.Conditions["amount"] = new List<AwsSqsFilterCondition>
		{
			new()
			{
				Operator = AwsSqsFilterOperator.Numeric,
				NumericComparison = ">=",
				Values = new List<object> { 100.0 },
			},
		};

		// Act
		var json = options.ToJson();

		// Assert
		json.ShouldContain("\"numeric\"");
		// Note: JSON encodes >= as \u003E= due to angle bracket encoding
		(json.Contains(">=") || json.Contains("\\u003E=")).ShouldBeTrue();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="AwsSqsFilterCondition"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFilterConditionShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void Constructor_HaveExactMatchOperator()
	{
		// Arrange & Act
		var condition = new AwsSqsFilterCondition();

		// Assert
		condition.Operator.ShouldBe(AwsSqsFilterOperator.ExactMatch);
	}

	[Fact]
	public void Constructor_HaveEmptyValues()
	{
		// Arrange & Act
		var condition = new AwsSqsFilterCondition();

		// Assert
		condition.Values.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_HaveNullNumericComparison()
	{
		// Arrange & Act
		var condition = new AwsSqsFilterCondition();

		// Assert
		condition.NumericComparison.ShouldBeNull();
	}

	#endregion

	#region ToJsonValue Tests

	[Fact]
	public void ToJsonValue_ReturnValueForSingleExactMatch()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.ExactMatch,
			Values = new List<object> { "high" },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		result.ShouldBe("high");
	}

	[Fact]
	public void ToJsonValue_ReturnListForMultipleExactMatch()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.ExactMatch,
			Values = new List<object> { "high", "urgent" },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<List<object>>();
		((List<object>)result).ShouldContain("high");
		((List<object>)result).ShouldContain("urgent");
	}

	[Fact]
	public void ToJsonValue_ReturnPrefixDictionary()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Prefix,
			Values = new List<object> { "us-" },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<Dictionary<string, object>>();
		((Dictionary<string, object>)result)["prefix"].ShouldBe("us-");
	}

	[Fact]
	public void ToJsonValue_ReturnSuffixDictionary()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Suffix,
			Values = new List<object> { ".com" },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<Dictionary<string, object>>();
		((Dictionary<string, object>)result)["suffix"].ShouldBe(".com");
	}

	[Fact]
	public void ToJsonValue_ReturnAnythingButDictionary()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.AnythingBut,
			Values = new List<object> { "deleted", "archived" },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<Dictionary<string, object>>();
		((Dictionary<string, object>)result).ShouldContainKey("anything-but");
	}

	[Fact]
	public void ToJsonValue_ReturnExistsDictionary()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Exists,
			Values = new List<object> { true },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<Dictionary<string, object>>();
		((Dictionary<string, object>)result)["exists"].ShouldBe(true);
	}

	[Fact]
	public void ToJsonValue_ReturnNumericDictionary()
	{
		// Arrange
		var condition = new AwsSqsFilterCondition
		{
			Operator = AwsSqsFilterOperator.Numeric,
			NumericComparison = ">=",
			Values = new List<object> { 100.0 },
		};

		// Act
		var result = condition.ToJsonValue();

		// Assert
		_ = result.ShouldBeOfType<Dictionary<string, object>>();
		((Dictionary<string, object>)result).ShouldContainKey("numeric");
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="AwsSqsFilterPolicyScope"/> enum.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFilterPolicyScopeShould : UnitTestBase
{
	[Fact]
	public void HaveMessageAttributesValue()
	{
		// Assert
		AwsSqsFilterPolicyScope.MessageAttributes.ShouldBe((AwsSqsFilterPolicyScope)0);
	}

	[Fact]
	public void HaveMessageBodyValue()
	{
		// Assert
		AwsSqsFilterPolicyScope.MessageBody.ShouldBe((AwsSqsFilterPolicyScope)1);
	}
}

/// <summary>
/// Unit tests for <see cref="AwsSqsFilterOperator"/> enum.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFilterOperatorShould : UnitTestBase
{
	[Theory]
	[InlineData(AwsSqsFilterOperator.ExactMatch, 0)]
	[InlineData(AwsSqsFilterOperator.Prefix, 1)]
	[InlineData(AwsSqsFilterOperator.Suffix, 2)]
	[InlineData(AwsSqsFilterOperator.AnythingBut, 3)]
	[InlineData(AwsSqsFilterOperator.Exists, 4)]
	[InlineData(AwsSqsFilterOperator.Numeric, 5)]
	public void HaveExpectedValues(AwsSqsFilterOperator @operator, int expectedValue)
	{
		// Assert
		((int)@operator).ShouldBe(expectedValue);
	}
}
