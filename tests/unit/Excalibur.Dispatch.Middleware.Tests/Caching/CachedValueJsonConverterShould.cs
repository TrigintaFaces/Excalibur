// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

[Trait("Category", "Unit")]
public sealed class CachedValueJsonConverterShould : UnitTestBase
{
	private readonly JsonSerializerOptions _jsonOptions;

	public CachedValueJsonConverterShould()
	{
		_jsonOptions = new JsonSerializerOptions
		{
			Converters = { new CachedValueJsonConverter() }
		};
	}

	[Fact]
	public void RoundTrip_WithAllPropertiesSet()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = "hello",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(string).AssemblyQualifiedName
		};

		// Act
		var json = JsonSerializer.Serialize(original, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
		deserialized.TypeName.ShouldBe(typeof(string).AssemblyQualifiedName);
		deserialized.Value.ShouldBe("hello");
	}

	[Fact]
	public void RoundTrip_WithNullValue()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = null,
			ShouldCache = false,
			HasExecuted = true,
			TypeName = null
		};

		// Act
		var json = JsonSerializer.Serialize(original, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBeNull();
		deserialized.ShouldCache.ShouldBeFalse();
		deserialized.HasExecuted.ShouldBeTrue();
		deserialized.TypeName.ShouldBeNull();
	}

	[Fact]
	public void RoundTrip_WithNullTypeName_ValueStaysAsJsonElement()
	{
		// Arrange — Value is non-null but TypeName is null, so Value should come back as JsonElement
		var original = new CachedValue
		{
			Value = 42,
			ShouldCache = true,
			HasExecuted = false,
			TypeName = null
		};

		// Act
		var json = JsonSerializer.Serialize(original, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		// Without TypeName, the value remains as JsonElement
		deserialized.Value.ShouldBeOfType<JsonElement>();
	}

	[Fact]
	public void RoundTrip_WithIntegerValue()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = 42,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(int).AssemblyQualifiedName
		};

		// Act
		var json = JsonSerializer.Serialize(original, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(42);
	}

	[Fact]
	public void Write_WithNullTypeName_OmitsTypeNameProperty()
	{
		// Arrange
		var value = new CachedValue
		{
			Value = "test",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = null
		};

		// Act
		var json = JsonSerializer.Serialize(value, _jsonOptions);

		// Assert — TypeName should not appear in JSON
		json.ShouldNotContain("TypeName");
	}

	[Fact]
	public void Write_WithNonNullTypeName_IncludesTypeNameProperty()
	{
		// Arrange
		var value = new CachedValue
		{
			Value = "test",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(string).AssemblyQualifiedName
		};

		// Act
		var json = JsonSerializer.Serialize(value, _jsonOptions);

		// Assert
		json.ShouldContain("TypeName");
		json.ShouldContain("System.String");
	}

	[Fact]
	public void Read_ThrowsJsonException_WhenNotStartObject()
	{
		// Arrange — JSON that is not an object
		var json = "\"hello\"";

		// Act & Assert
		Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions));
	}

	[Fact]
	public void Read_HandlesUnknownProperties_BySkipping()
	{
		// Arrange — JSON with extra unknown properties
		var json = """{"ShouldCache":true,"HasExecuted":false,"UnknownProp":"value","AnotherOne":123,"Value":null}""";

		// Act
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeFalse();
		deserialized.Value.ShouldBeNull();
	}

	[Fact]
	public void Read_WithValueBeforeTypeName_DeserializesCorrectly()
	{
		// Arrange — Value property appears before TypeName in JSON
		// The converter reads Value as JsonElement first, then deserializes after reading TypeName
		var json = $$$"""{"Value":"deferred","ShouldCache":true,"HasExecuted":true,"TypeName":"{{{typeof(string).AssemblyQualifiedName}}}"}""";

		// Act
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe("deferred");
		deserialized.TypeName.ShouldNotBeNull();
	}

	[Fact]
	public void Read_WithInvalidTypeName_ValueRemainsAsJsonElement()
	{
		// Arrange — TypeName is a type that doesn't exist
		var json = """{"ShouldCache":true,"HasExecuted":true,"TypeName":"NonExistent.Type, NonExistent","Value":"test"}""";

		// Act
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert — Type.GetType returns null, so value stays as JsonElement
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBeOfType<JsonElement>();
	}

	[Fact]
	public void RoundTrip_WithComplexObject()
	{
		// Arrange
		var complexValue = new TestComplexValue { Name = "test", Count = 5 };
		var original = new CachedValue
		{
			Value = complexValue,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(TestComplexValue).AssemblyQualifiedName
		};

		// Act
		var json = JsonSerializer.Serialize(original, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, _jsonOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		var resultValue = deserialized.Value.ShouldBeOfType<TestComplexValue>();
		resultValue.Name.ShouldBe("test");
		resultValue.Count.ShouldBe(5);
	}

	[Fact]
	public void Write_WithNullValue_WritesNullToken()
	{
		// Arrange
		var value = new CachedValue
		{
			Value = null,
			ShouldCache = false,
			HasExecuted = false,
			TypeName = null
		};

		// Act
		var json = JsonSerializer.Serialize(value, _jsonOptions);

		// Assert
		json.ShouldContain("null");
	}

	internal sealed class TestComplexValue
	{
		public string Name { get; set; } = "";
		public int Count { get; set; }
	}
}
