// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

[UnitTest]
public sealed class ElasticIndexMappingBuilderShould
{
	// ─── Tier 1: Explicit mapping via IElasticIndexConfiguration ─────────

	[Fact]
	public void UseExplicitMapping_WhenTypeImplementsIElasticIndexConfiguration()
	{
		// Act
		var properties = ElasticIndexMappingBuilder.BuildMappingProperties<ExplicitMappingDocument>();

		// Assert — should contain the explicitly declared fields
		properties.ShouldContainKey("orderId");
		properties["orderId"].ShouldBeOfType<KeywordProperty>();

		properties.ShouldContainKey("customerName");
		properties["customerName"].ShouldBeOfType<TextProperty>();

		properties.ShouldContainKey("totalAmount");
		properties["totalAmount"].ShouldBeOfType<DoubleNumberProperty>();
	}

	[Fact]
	public void NotIncludeReflectionMappings_WhenExplicitMappingIsProvided()
	{
		// The ExplicitMappingDocument has a public CreatedAt property
		// that is NOT included in ConfigureIndex(). It should NOT appear.
		var properties = ElasticIndexMappingBuilder.BuildMappingProperties<ExplicitMappingDocument>();

		// Only the 3 explicitly mapped fields should be present
		properties.Count().ShouldBe(3);
		properties.ShouldNotContainKey("createdAt");
	}

	// ─── Tier 2: Reflection-inferred mapping ────────────────────────────

	[Fact]
	public void MapStringProperties_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("name");
		properties["name"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapGuidProperties_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("id");
		properties["id"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapEnumProperties_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("status");
		properties["status"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapIntProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("count");
		properties["count"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapLongProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("bigNumber");
		properties["bigNumber"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapShortProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("shortValue");
		properties["shortValue"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapByteProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("byteValue");
		properties["byteValue"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapFloatProperties_ToDouble()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("score");
		properties["score"].ShouldBeOfType<DoubleNumberProperty>();
	}

	[Fact]
	public void MapDoubleProperties_ToDouble()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("rating");
		properties["rating"].ShouldBeOfType<DoubleNumberProperty>();
	}

	[Fact]
	public void MapDecimalProperties_ToDouble()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("amount");
		properties["amount"].ShouldBeOfType<DoubleNumberProperty>();
	}

	[Fact]
	public void MapDateTimeProperties_ToDate()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("createdAt");
		properties["createdAt"].ShouldBeOfType<DateProperty>();
	}

	[Fact]
	public void MapDateTimeOffsetProperties_ToDate()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("updatedAt");
		properties["updatedAt"].ShouldBeOfType<DateProperty>();
	}

	[Fact]
	public void MapDateOnlyProperties_ToDate()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<DateTypesDocument>();

		properties.ShouldContainKey("birthDate");
		properties["birthDate"].ShouldBeOfType<DateProperty>();
	}

	[Fact]
	public void MapBoolProperties_ToBoolean()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("isActive");
		properties["isActive"].ShouldBeOfType<BooleanProperty>();
	}

	[Fact]
	public void MapStringList_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<InferredDocument>();

		properties.ShouldContainKey("tags");
		properties["tags"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapStringArray_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<CollectionTypesDocument>();

		properties.ShouldContainKey("categories");
		properties["categories"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapHashSetOfString_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<CollectionTypesDocument>();

		properties.ShouldContainKey("uniqueTags");
		properties["uniqueTags"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapIReadOnlyListOfString_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<CollectionTypesDocument>();

		properties.ShouldContainKey("readOnlyItems");
		properties["readOnlyItems"].ShouldBeOfType<KeywordProperty>();
	}

	// ─── Nullable type handling ─────────────────────────────────────────

	[Fact]
	public void MapNullableInt_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NullableTypesDocument>();

		properties.ShouldContainKey("nullableCount");
		properties["nullableCount"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapNullableBool_ToBoolean()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NullableTypesDocument>();

		properties.ShouldContainKey("nullableFlag");
		properties["nullableFlag"].ShouldBeOfType<BooleanProperty>();
	}

	[Fact]
	public void MapNullableDateTime_ToDate()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NullableTypesDocument>();

		properties.ShouldContainKey("nullableDate");
		properties["nullableDate"].ShouldBeOfType<DateProperty>();
	}

	[Fact]
	public void MapNullableGuid_ToKeyword()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NullableTypesDocument>();

		properties.ShouldContainKey("nullableId");
		properties["nullableId"].ShouldBeOfType<KeywordProperty>();
	}

	[Fact]
	public void MapNullableDecimal_ToDouble()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NullableTypesDocument>();

		properties.ShouldContainKey("nullableAmount");
		properties["nullableAmount"].ShouldBeOfType<DoubleNumberProperty>();
	}

	// ─── Complex type handling ──────────────────────────────────────────

	[Fact]
	public void SkipComplexNestedTypes_ForDynamicMapping()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<DocumentWithNestedType>();

		// Simple properties should be mapped
		properties.ShouldContainKey("id");

		// Complex nested type should be skipped (ES dynamic mapping handles it)
		properties.ShouldNotContainKey("address");
	}

	[Fact]
	public void SkipDictionaryProperties_ForDynamicMapping()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<DocumentWithDictionary>();

		properties.ShouldContainKey("name");
		properties.ShouldNotContainKey("metadata");
	}

	[Fact]
	public void SkipNonStringCollections_ForDynamicMapping()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<DocumentWithNonStringCollection>();

		properties.ShouldContainKey("name");
		// List<int> is not a string collection — should be skipped
		properties.ShouldNotContainKey("scores");
	}

	// ─── JSON property name handling ────────────────────────────────────

	[Fact]
	public void UseJsonPropertyName_WhenAttributeIsPresent()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<JsonNameDocument>();

		// Uses the [JsonPropertyName] value, not the C# property name
		properties.ShouldContainKey("full_name");
		properties.ShouldNotContainKey("fullName");
		properties.ShouldNotContainKey("FullName");
	}

	[Fact]
	public void UseCamelCase_WhenNoJsonPropertyNameAttribute()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<JsonNameDocument>();

		// PascalCase "OrderCount" becomes camelCase "orderCount"
		properties.ShouldContainKey("orderCount");
		properties.ShouldNotContainKey("OrderCount");
	}

	// ─── Edge cases ─────────────────────────────────────────────────────

	[Fact]
	public void ReturnEmptyProperties_ForEmptyDocument()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<EmptyDocument>();

		properties.Count().ShouldBe(0);
	}

	[Fact]
	public void SkipWriteOnlyProperties()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<WriteOnlyPropertyDocument>();

		// Write-only property should be skipped (no getter)
		properties.ShouldNotContainKey("writeOnly");
		// Normal property should still be mapped
		properties.ShouldContainKey("name");
	}

	[Fact]
	public void NotIncludeStaticProperties()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<StaticPropertyDocument>();

		// Static properties should not be included (only instance properties)
		properties.ShouldNotContainKey("staticValue");
		properties.ShouldContainKey("instanceValue");
	}

	[Fact]
	public void BuildMappingProperties_FallsBackToInferred_WhenNotExplicit()
	{
		// BuildMappingProperties should use inferred mapping for types
		// that don't implement IElasticIndexConfiguration
		var properties = ElasticIndexMappingBuilder.BuildMappingProperties<InferredDocument>();

		// Should have inferred mappings
		properties.ShouldContainKey("name");
		properties["name"].ShouldBeOfType<KeywordProperty>();
		properties.ShouldContainKey("count");
		properties["count"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void BuildMappingProperties_UsesExplicit_WhenAvailable()
	{
		var properties = ElasticIndexMappingBuilder.BuildMappingProperties<ExplicitMappingDocument>();

		// Should use explicit mapping, not inferred
		properties.ShouldContainKey("customerName");
		properties["customerName"].ShouldBeOfType<TextProperty>();
	}

	// ─── Unsigned integer types ─────────────────────────────────────────

	[Fact]
	public void MapUintProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("uintValue");
		properties["uintValue"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapUlongProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("ulongValue");
		properties["ulongValue"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapUshortProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("ushortValue");
		properties["ushortValue"].ShouldBeOfType<LongNumberProperty>();
	}

	[Fact]
	public void MapSbyteProperties_ToLong()
	{
		var properties = ElasticIndexMappingBuilder.BuildInferredMapping<NumericTypesDocument>();

		properties.ShouldContainKey("sbyteValue");
		properties["sbyteValue"].ShouldBeOfType<LongNumberProperty>();
	}

	// ─── Test document types ────────────────────────────────────────────

	private sealed class ExplicitMappingDocument : IElasticIndexConfiguration<ExplicitMappingDocument>
	{
		public Guid OrderId { get; set; }
		public string CustomerName { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public DateTimeOffset CreatedAt { get; set; } // Not in explicit mapping

		public static Properties ConfigureIndex() => new()
		{
			{ "orderId", new KeywordProperty() },
			{ "customerName", new TextProperty() },
			{ "totalAmount", new DoubleNumberProperty() }
		};
	}

	private sealed class InferredDocument
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Count { get; set; }
		public long BigNumber { get; set; }
		public float Score { get; set; }
		public double Rating { get; set; }
		public decimal Amount { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTimeOffset UpdatedAt { get; set; }
		public bool IsActive { get; set; }
		public List<string> Tags { get; set; } = [];
		public TestStatus Status { get; set; }
	}

	private sealed class NumericTypesDocument
	{
		public byte ByteValue { get; set; }
		public sbyte SbyteValue { get; set; }
		public short ShortValue { get; set; }
		public ushort UshortValue { get; set; }
		public uint UintValue { get; set; }
		public ulong UlongValue { get; set; }
	}

	private sealed class DateTypesDocument
	{
		public DateOnly BirthDate { get; set; }
	}

	private sealed class CollectionTypesDocument
	{
		public string[] Categories { get; set; } = [];
		public HashSet<string> UniqueTags { get; set; } = [];
		public IReadOnlyList<string> ReadOnlyItems { get; set; } = [];
	}

	private sealed class NullableTypesDocument
	{
		public int? NullableCount { get; set; }
		public bool? NullableFlag { get; set; }
		public DateTime? NullableDate { get; set; }
		public Guid? NullableId { get; set; }
		public decimal? NullableAmount { get; set; }
	}

	private sealed class DocumentWithNestedType
	{
		public string Id { get; set; } = string.Empty;
		public AddressNested Address { get; set; } = new();
	}

	private sealed class AddressNested
	{
		public string Street { get; set; } = string.Empty;
		public string City { get; set; } = string.Empty;
	}

	private sealed class DocumentWithDictionary
	{
		public string Name { get; set; } = string.Empty;
		public Dictionary<string, string> Metadata { get; set; } = [];
	}

	private sealed class DocumentWithNonStringCollection
	{
		public string Name { get; set; } = string.Empty;
		public List<int> Scores { get; set; } = [];
	}

	private sealed class JsonNameDocument
	{
		[JsonPropertyName("full_name")]
		public string FullName { get; set; } = string.Empty;

		// No attribute — should become camelCase "orderCount"
		public int OrderCount { get; set; }
	}

	private sealed class EmptyDocument;

	private sealed class WriteOnlyPropertyDocument
	{
		public string Name { get; set; } = string.Empty;

#pragma warning disable CA1822 // Mark members as static - test needs instance property
		public string WriteOnly { set { /* no-op */ } }
#pragma warning restore CA1822
	}

	private sealed class StaticPropertyDocument
	{
		public static string StaticValue { get; set; } = string.Empty;
		public string InstanceValue { get; set; } = string.Empty;
	}

	private enum TestStatus
	{
		Active,
		Inactive
	}
}
