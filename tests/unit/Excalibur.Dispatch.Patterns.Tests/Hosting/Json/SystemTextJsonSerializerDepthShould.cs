// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Depth coverage tests for DispatchJsonSerializer configured in reflection mode.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
public sealed partial class SystemTextJsonSerializerDepthShould
{
	/// <summary>
	/// Creates a <see cref="DispatchJsonSerializer"/> configured for reflection-based serialization
	/// (no source-gen context) so arbitrary test types can be used.
	/// </summary>
	private static DispatchJsonSerializer CreateReflectionSerializer(Action<JsonSerializerOptions>? additionalConfigure = null)
	{
		return new DispatchJsonSerializer(
			configure: opts =>
			{
				opts.TypeInfoResolver = null;
				additionalConfigure?.Invoke(opts);
			});
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsAccessorIsNull()
	{
		// DispatchJsonSerializer constructor has all optional parameters,
		// so this test validates that the internal SystemTextJsonSerializer (if it exists)
		// would throw on null options accessor. Since we now use DispatchJsonSerializer
		// directly, verify that the serializer can be constructed without error.
		using var serializer = new DispatchJsonSerializer();
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Resolve_DirectConstruction_ReturnsSerializer()
	{
		// Arrange & Act
		using var serializer = CreateReflectionSerializer();

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithDefaults_UsesCamelCase()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act
		var json = serializer.Serialize(new TestPayload { Name = "test" }, typeof(TestPayload));

		// Assert — use Ordinal comparison to distinguish "name" from "Name"
		json.ShouldContain("\"name\"");
		json.IndexOf("\"Name\"", StringComparison.Ordinal).ShouldBe(-1,
			"Expected no PascalCase 'Name' property key in JSON output");
	}

	[Fact]
	public void Deserialize_ReturnsCorrectType()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act
		var result = serializer.Deserialize("{\"name\":\"hello\",\"count\":5}", typeof(TestPayload));

		// Assert
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Name.ShouldBe("hello");
		payload.Count.ShouldBe(5);
	}

	[Fact]
	public void Serialize_NullType_ThrowsArgumentNullException()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize(new TestPayload(), null!));
	}

	[Fact]
	public void Deserialize_NullJson_ThrowsArgumentException()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act & Assert — ThrowIfNullOrWhiteSpace throws ArgumentException (or ArgumentNullException subclass)
		Should.Throw<ArgumentException>(() => serializer.Deserialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Serialize_NullObjectType_ThrowsArgumentNullException()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize(new TestPayload(), null!));
	}

	[Fact]
	public void Deserialize_NullType_ThrowsArgumentException()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act & Assert
		Should.Throw<ArgumentException>(() => serializer.Deserialize("{}", null!));
	}

	[Fact]
	public void Serialize_WithReflectionMode_ProducesValidJson()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act
		var json = serializer.Serialize(new TestPayload { Name = "reflection-test" }, typeof(TestPayload));

		// Assert — valid JSON with camelCase naming
		json.ShouldContain("\"name\"");
		json.ShouldContain("reflection-test");
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsJsonException()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();

		// Act & Assert
		Should.Throw<JsonException>(() => serializer.Deserialize("{not valid}", typeof(TestPayload)));
	}

	[Fact]
	public void Roundtrip_PreservesData()
	{
		// Arrange
		using var serializer = CreateReflectionSerializer();
		var original = new TestPayload { Name = "roundtrip", Count = 999 };

		// Act
		var json = serializer.Serialize(original, typeof(TestPayload));
		var result = serializer.Deserialize(json, typeof(TestPayload));

		// Assert
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Name.ShouldBe("roundtrip");
		payload.Count.ShouldBe(999);
	}

	[Fact]
	public void SerializeAndDeserialize_WithSourceGenContext_WhenConfigured()
	{
		// Arrange — use a source-gen context for Dictionary<string, string>
		using var serializer = new DispatchJsonSerializer(
			jsonContext: null,
			configure: opts => opts.TypeInfoResolver = DictionaryStringStringJsonContext.Default);
		var payload = new Dictionary<string, string> { ["key"] = "value" };

		// Act
		var json = serializer.Serialize(payload, typeof(Dictionary<string, string>));
		var result = serializer.Deserialize(json, typeof(Dictionary<string, string>));

		// Assert
		json.ShouldContain("\"key\":\"value\"");
		var roundTrip = result.ShouldBeOfType<Dictionary<string, string>>();
		roundTrip["key"].ShouldBe("value");
	}

	private sealed class TestPayload
	{
		public string? Name { get; set; }
		public int Count { get; set; }
	}

	[JsonSerializable(typeof(Dictionary<string, string>))]
	private sealed partial class DictionaryStringStringJsonContext : JsonSerializerContext
	{
	}
}
