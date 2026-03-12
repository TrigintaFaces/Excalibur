// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Unit tests for DispatchJsonSerializer configured in reflection mode (no source-gen context)
/// so that arbitrary test types can be serialized and deserialized.
/// </summary>
[Trait("Category", "Unit")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code — trimming not required")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code — AOT not required")]
public sealed class SystemTextJsonSerializerShould : IDisposable
{
	private readonly DispatchJsonSerializer _serializer;

	public SystemTextJsonSerializerShould()
	{
		// Use reflection-based serialization by clearing the source-gen TypeInfoResolver.
		// This allows the serializer to handle arbitrary test types like TestPayload.
		_serializer = new DispatchJsonSerializer(
			configure: opts => opts.TypeInfoResolver = null);
	}

	public void Dispose() => _serializer.Dispose();

	[Fact]
	public void Resolve_Serializer_IsNotNull()
	{
		// Assert
		_serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_SimpleObject_ReturnsValidJson()
	{
		// Arrange
		var value = new TestPayload { Name = "test", Count = 42 };

		// Act
		var json = _serializer.Serialize(value);

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("\"name\""); // camelCase by default
		json.ShouldContain("\"count\"");
	}

	[Fact]
	public void Deserialize_ValidJson_ReturnsObject()
	{
		// Arrange
		var json = """{"name":"test","count":42}""";

		// Act
		var result = _serializer.Deserialize<TestPayload>(json);

		// Assert
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Name.ShouldBe("test");
		payload.Count.ShouldBe(42);
	}

	[Fact]
	public void Roundtrip_PreservesValues()
	{
		// Arrange
		var original = new TestPayload { Name = "roundtrip", Count = 99 };

		// Act
		var json = _serializer.Serialize(original);
		var deserialized = _serializer.Deserialize<TestPayload>(json);

		// Assert
		var result = deserialized.ShouldBeOfType<TestPayload>();
		result.Name.ShouldBe(original.Name);
		result.Count.ShouldBe(original.Count);
	}

	[Fact]
	public void Serialize_WithCustomConfigure_AppliesOptions()
	{
		// Arrange — create serializer with reflection mode (no source-gen context)
		using var serializer = new DispatchJsonSerializer(
			configure: opts =>
			{
				opts.TypeInfoResolver = null;
			});
		var value = new TestPayload { Name = "custom", Count = 42 };

		// Act
		var json = serializer.Serialize(value);

		// Assert — serializer produces valid JSON with camelCase (default)
		json.ShouldContain("\"name\"");
		json.ShouldContain("\"count\"");
		json.ShouldContain("42");
	}

	[Fact]
	public void Serialize_NullType_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _serializer.Serialize(new TestPayload(), null!));
	}

	[Fact]
	public void Deserialize_NullJson_ThrowsArgumentException()
	{
		// Act & Assert — ThrowIfNullOrWhiteSpace throws ArgumentException (or ArgumentNullException subclass)
		Should.Throw<ArgumentException>(() => _serializer.Deserialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Deserialize_NullType_ThrowsArgumentException()
	{
		// Act & Assert — ThrowIfNullOrWhiteSpace on empty string throws, then ThrowIfNull on type
		Should.Throw<ArgumentNullException>(() => _serializer.Deserialize("{}", null!));
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsJsonException()
	{
		// Act & Assert
		Should.Throw<JsonException>(() => _serializer.Deserialize<TestPayload>("not valid json"));
	}

	[Fact]
	public void Deserialize_CaseInsensitive_ByDefault()
	{
		// Arrange — PascalCase keys should still deserialize due to PropertyNameCaseInsensitive = true
		var json = """{"Name":"pascal","Count":7}""";

		// Act
		var result = _serializer.Deserialize<TestPayload>(json);

		// Assert
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Name.ShouldBe("pascal");
		payload.Count.ShouldBe(7);
	}

	/// <summary>
	/// Simple DTO for serialization tests.
	/// </summary>
	private sealed class TestPayload
	{
		public string? Name { get; set; }
		public int Count { get; set; }
	}
}
