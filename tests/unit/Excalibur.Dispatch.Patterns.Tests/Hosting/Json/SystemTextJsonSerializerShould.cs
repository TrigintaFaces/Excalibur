// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Tests.Shared;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Unit tests for the internal SystemTextJsonSerializer resolved via DI.
/// </summary>
[Trait("Category", "Unit")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code — trimming not required")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code — AOT not required")]
public sealed class SystemTextJsonSerializerShould : UnitTestBase
{
	private readonly IJsonSerializer _serializer;

	public SystemTextJsonSerializerShould()
	{
		_ = Services.AddJsonSerialization();
		BuildServiceProvider();
		_serializer = GetRequiredService<IJsonSerializer>();
	}

	[Fact]
	public void Resolve_FromDI_AsIJsonSerializer()
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
		var json = _serializer.Serialize(value, typeof(TestPayload));

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("\"name\""); // camelCase by default (Web defaults)
		json.ShouldContain("\"count\"");
	}

	[Fact]
	public void Deserialize_ValidJson_ReturnsObject()
	{
		// Arrange
		var json = """{"name":"test","count":42}""";

		// Act
		var result = _serializer.Deserialize(json, typeof(TestPayload));

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
		var json = _serializer.Serialize(original, typeof(TestPayload));
		var deserialized = _serializer.Deserialize(json, typeof(TestPayload));

		// Assert
		var result = deserialized.ShouldBeOfType<TestPayload>();
		result.Name.ShouldBe(original.Name);
		result.Count.ShouldBe(original.Count);
	}

	[Fact]
	public void Serialize_WithCustomOptions_RespectsConfiguration()
	{
		// Arrange — rebuild with indented output
		var services = new ServiceCollection();
		_ = services.AddJsonSerialization(opt => opt.SerializerOptions.WriteIndented = true);
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();
		var value = new TestPayload { Name = "indented", Count = 1 };

		// Act
		var json = serializer.Serialize(value, typeof(TestPayload));

		// Assert — indented JSON contains newlines
		json.ShouldContain("\n");
	}

	[Fact]
	public void Serialize_ThrowsOnNullValue()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _serializer.Serialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Serialize_ThrowsOnNullType()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _serializer.Serialize(new TestPayload(), null!));
	}

	[Fact]
	public void Deserialize_ThrowsOnNullJson()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _serializer.Deserialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Deserialize_ThrowsOnNullType()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _serializer.Deserialize("{}", null!));
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsJsonException()
	{
		// Act & Assert
		Should.Throw<JsonException>(() => _serializer.Deserialize("not valid json", typeof(TestPayload)));
	}

	[Fact]
	public void Deserialize_CaseInsensitive_ByDefault()
	{
		// Arrange — PascalCase keys should still deserialize due to Web defaults
		var json = """{"Name":"pascal","Count":7}""";

		// Act
		var result = _serializer.Deserialize(json, typeof(TestPayload));

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
