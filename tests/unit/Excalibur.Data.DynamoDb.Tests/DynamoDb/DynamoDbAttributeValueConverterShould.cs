// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using StreamsAttributeValue = Amazon.DynamoDBStreams.Model.AttributeValue;
using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for DynamoDbAttributeValueConverter.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify attribute value conversion between Streams and DynamoDB SDK types.
/// Note: DynamoDbAttributeValueConverter is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Converter")]
public sealed class DynamoDbAttributeValueConverterShould
{
	private readonly Type _converterType;
	private readonly MethodInfo _toAttributeValueMapMethod;

	public DynamoDbAttributeValueConverterShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbOptions).Assembly;
		_converterType = assembly.GetType("Excalibur.Data.DynamoDb.DynamoDbAttributeValueConverter")!;
		_toAttributeValueMapMethod = _converterType.GetMethod(
			"ToAttributeValueMap",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
	}

	#region ToAttributeValueMap Tests

	[Fact]
	public void ToAttributeValueMap_ReturnsNull_WhenInputIsNull()
	{
		// Act
		var result = _toAttributeValueMapMethod.Invoke(
			null,
			new object?[] { null });

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ToAttributeValueMap_ReturnsEmptyDictionary_WhenInputIsEmpty()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>();

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsStringAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["name"] = new() { S = "test-value" }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("name");
		result["name"].S.ShouldBe("test-value");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsNumberAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["count"] = new() { N = "42" }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("count");
		result["count"].N.ShouldBe("42");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsBoolAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["active"] = new() { BOOL = true }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("active");
		result["active"].BOOL.ShouldBe(true);
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsNullAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["empty"] = new() { NULL = true }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("empty");
		result["empty"].NULL.ShouldBe(true);
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsStringSetAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["tags"] = new() { SS = ["tag1", "tag2"] }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("tags");
		result["tags"].SS.ShouldContain("tag1");
		result["tags"].SS.ShouldContain("tag2");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsNumberSetAttribute()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["scores"] = new() { NS = ["1", "2", "3"] }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("scores");
		result["scores"].NS.ShouldContain("1");
		result["scores"].NS.ShouldContain("2");
		result["scores"].NS.ShouldContain("3");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsNestedMapAttribute()
	{
		// Arrange
		var nestedMap = new Dictionary<string, StreamsAttributeValue>
		{
			["nested_key"] = new() { S = "nested_value" }
		};
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["metadata"] = new() { M = nestedMap }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("metadata");
		result["metadata"].M.ShouldContainKey("nested_key");
		result["metadata"].M["nested_key"].S.ShouldBe("nested_value");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsListAttribute()
	{
		// Arrange
		var list = new List<StreamsAttributeValue>
		{
			new() { S = "item1" },
			new() { S = "item2" }
		};
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["items"] = new() { L = list }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.ShouldContainKey("items");
		result["items"].L.Count.ShouldBe(2);
		result["items"].L[0].S.ShouldBe("item1");
		result["items"].L[1].S.ShouldBe("item2");
	}

	[Fact]
	public void ToAttributeValueMap_ConvertsMultipleAttributes()
	{
		// Arrange
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["pk"] = new() { S = "pk-123" },
			["sk"] = new() { S = "sk-456" },
			["count"] = new() { N = "10" },
			["active"] = new() { BOOL = true }
		};

		// Act
		var result = (Dictionary<string, DynamoDbAttributeValue>)_toAttributeValueMapMethod.Invoke(
			null,
			new object[] { input })!;

		// Assert
		result.Count.ShouldBe(4);
		result["pk"].S.ShouldBe("pk-123");
		result["sk"].S.ShouldBe("sk-456");
		result["count"].N.ShouldBe("10");
		result["active"].BOOL.ShouldBe(true);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		_converterType.IsAbstract.ShouldBeTrue();
		_converterType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_converterType.IsNotPublic.ShouldBeTrue();
	}

	#endregion
}
