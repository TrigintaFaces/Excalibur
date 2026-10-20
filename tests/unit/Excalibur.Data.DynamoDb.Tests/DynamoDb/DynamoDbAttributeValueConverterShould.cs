// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text.Json;

using Amazon.DynamoDBv2.DocumentModel;

using StreamsAttributeValue = Amazon.DynamoDBStreams.Model.AttributeValue;
using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for DynamoDbAttributeValueConverter.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify attribute value conversion between Streams and DynamoDB SDK types.
/// The converter is public; new SDK serialization controls exercise it directly.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Converter")]
[Trait("Pattern", "SERIALIZATION")]
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
	public void PreserveNullEmptyAndNonemptyContainersThroughTheActualSdkJsonWriter()
	{
		var input = new Dictionary<string, StreamsAttributeValue>(StringComparer.Ordinal)
		{
			["null"] = new() { NULL = true },
			["emptyMap"] = new() { M = new Dictionary<string, StreamsAttributeValue>() },
			["emptyList"] = new() { L = [] },
			["map"] = new() { M = new Dictionary<string, StreamsAttributeValue> { ["inside"] = new() { S = "map-value" } } },
			["list"] = new() { L = [new() { S = "list-value" }] },
		};
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(input).ShouldNotBeNull();
		using var json = JsonDocument.Parse(Document.FromAttributeMap(converted).ToJson());
		json.RootElement.GetProperty("null").ValueKind.ShouldBe(JsonValueKind.Null);
		json.RootElement.GetProperty("emptyMap").ValueKind.ShouldBe(JsonValueKind.Object);
		json.RootElement.GetProperty("emptyMap").EnumerateObject().ShouldBeEmpty();
		json.RootElement.GetProperty("emptyList").ValueKind.ShouldBe(JsonValueKind.Array);
		json.RootElement.GetProperty("emptyList").GetArrayLength().ShouldBe(0);
		json.RootElement.GetProperty("map").GetProperty("inside").GetString().ShouldBe("map-value");
		json.RootElement.GetProperty("list")[0].GetString().ShouldBe("list-value");
	}

	[Fact]
	public void PreserveNestedEmptyContainersAndFalseBooleanThroughTheActualSdkJsonWriter()
	{
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(new Dictionary<string, StreamsAttributeValue>
		{
			["outer"] = new()
			{
				M = new Dictionary<string, StreamsAttributeValue>
				{
					["sequence"] = new() { L = [new() { M = new Dictionary<string, StreamsAttributeValue>() },
						new() { L = [] }, new() { BOOL = false }] },
				},
			},
		}).ShouldNotBeNull();
		using var json = JsonDocument.Parse(Document.FromAttributeMap(converted).ToJson());
		var sequence = json.RootElement.GetProperty("outer").GetProperty("sequence");
		sequence.GetArrayLength().ShouldBe(3);
		sequence[0].ValueKind.ShouldBe(JsonValueKind.Object);
		sequence[0].EnumerateObject().ShouldBeEmpty();
		sequence[1].ValueKind.ShouldBe(JsonValueKind.Array);
		sequence[1].GetArrayLength().ShouldBe(0);
		sequence[2].GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public void KeepUnsetContainersUnset()
	{
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(new Dictionary<string, StreamsAttributeValue>
		{
			["scalar"] = new() { S = "declared", M = null, L = null },
		}).ShouldNotBeNull();
		converted["scalar"].M.ShouldBeNull();
		converted["scalar"].L.ShouldBeNull();
		converted["scalar"].S.ShouldBe("declared");
	}

	[Fact]
	public void RetainDeclaredScalarAndNonemptyKindsWhileIgnoringCompetingEmptyCollections()
	{
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["emptyString"] = WithEmptyCollections(new StreamsAttributeValue { S = string.Empty }),
			["false"] = WithEmptyCollections(new StreamsAttributeValue { BOOL = false }),
			["number"] = WithEmptyCollections(new StreamsAttributeValue { N = "123.50" }),
			["stringSet"] = WithEmptyCollections(new StreamsAttributeValue { SS = ["member"] }),
			["map"] = new() { M = new Dictionary<string, StreamsAttributeValue> { ["key"] = new() { S = "value" } }, L = [] },
			["list"] = new() { M = new Dictionary<string, StreamsAttributeValue>(), L = [new() { S = "entry" }] },
		};
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(input).ShouldNotBeNull();
		using var json = JsonDocument.Parse(Document.FromAttributeMap(converted).ToJson());
		json.RootElement.GetProperty("emptyString").GetString().ShouldBe(string.Empty);
		json.RootElement.GetProperty("false").GetBoolean().ShouldBeFalse();
		json.RootElement.GetProperty("number").GetDecimal().ShouldBe(123.50m);
		json.RootElement.GetProperty("stringSet")[0].GetString().ShouldBe("member");
		json.RootElement.GetProperty("map").GetProperty("key").GetString().ShouldBe("value");
		json.RootElement.GetProperty("list")[0].GetString().ShouldBe("entry");
		converted["emptyString"].M.ShouldBeNull();
		converted["emptyString"].L.ShouldBeNull();
		converted["emptyString"].SS.ShouldBeNull();
	}

	[Fact]
	public void RefuseAnUnidentifiedValueWithBothEmptyContainerKinds()
	{
		var input = new Dictionary<string, StreamsAttributeValue>
		{
			["ambiguous"] = WithEmptyCollections(new StreamsAttributeValue()),
		};
		Should.Throw<InvalidOperationException>(() => DynamoDbAttributeValueConverter.ToAttributeValueMap(input))
			.Message.ShouldContain("AWSConfigs.InitializeCollections = false");
	}

	private static StreamsAttributeValue WithEmptyCollections(StreamsAttributeValue value)
	{
		value.M = new Dictionary<string, StreamsAttributeValue>();
		value.L = [];
		value.BS ??= [];
		value.NS ??= [];
		value.SS ??= [];
		return value;
	}

	[Fact]
	public void IsStatic()
	{
		// Assert
		_converterType.IsAbstract.ShouldBeTrue();
		_converterType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		_converterType.IsPublic.ShouldBeTrue();
	}

	#endregion
}
