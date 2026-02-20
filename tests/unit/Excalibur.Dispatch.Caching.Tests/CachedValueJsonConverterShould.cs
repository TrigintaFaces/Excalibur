// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachedValueJsonConverterShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new CachedValueJsonConverter() },
	};

	[Fact]
	public void RoundTrip_WithStringValue()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = "hello world",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(string).AssemblyQualifiedName,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe("hello world");
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
	}

	[Fact]
	public void RoundTrip_WithIntValue()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = 42,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(int).AssemblyQualifiedName,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
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
			TypeName = null,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBeNull();
		deserialized.ShouldCache.ShouldBeFalse();
		deserialized.HasExecuted.ShouldBeTrue();
	}

	[Fact]
	public void RoundTrip_WithComplexType()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = new TestDto { Id = 42, Name = "Test" },
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(TestDto).AssemblyQualifiedName,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
		deserialized.Value.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithoutTypeName_WhenTypeNameIsNull()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = "test",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = null,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);

		// Assert
		json.ShouldNotContain("TypeName");
	}

	[Fact]
	public void ThrowJsonException_WhenNotStartObject()
	{
		// Act & Assert
		Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<CachedValue>("\"not-an-object\"", Options));
	}

	[Fact]
	public void HandleUnknownProperties_Gracefully()
	{
		// Arrange
		var json = """
		{
			"ShouldCache": true,
			"HasExecuted": false,
			"UnknownProperty": "ignored",
			"Value": null
		}
		""";

		// Act
		var result = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldCache.ShouldBeTrue();
		result.HasExecuted.ShouldBeFalse();
	}

	[Fact]
	public void PreserveAllBooleanFlags()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = null,
			ShouldCache = true,
			HasExecuted = true,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
	}

	[Fact]
	public void HandleFalseFlags()
	{
		// Arrange
		var original = new CachedValue
		{
			Value = null,
			ShouldCache = false,
			HasExecuted = false,
		};

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeFalse();
		deserialized.HasExecuted.ShouldBeFalse();
	}

	[Fact]
	public void Deserialize_JsonWithAllFields()
	{
		// Arrange
		var json = """
		{
			"ShouldCache": true,
			"HasExecuted": true,
			"TypeName": "System.String, System.Private.CoreLib",
			"Value": "test-value"
		}
		""";

		// Act
		var result = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldCache.ShouldBeTrue();
		result.HasExecuted.ShouldBeTrue();
		result.TypeName.ShouldNotBeNull();
		result.Value.ShouldBe("test-value");
	}

	[Fact]
	public void Deserialize_JsonWithInvalidTypeName_FallsBackToJsonElement()
	{
		// Arrange
		var json = """
		{
			"ShouldCache": true,
			"HasExecuted": true,
			"TypeName": "NonExistent.Type, NonExistent.Assembly",
			"Value": {"Key": "fallback"}
		}
		""";

		// Act
		var result = JsonSerializer.Deserialize<CachedValue>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldCache.ShouldBeTrue();
		// Value should be a JsonElement since the type couldn't be resolved
		result.Value.ShouldBeOfType<JsonElement>();
	}

	private sealed class TestDto
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}
}
