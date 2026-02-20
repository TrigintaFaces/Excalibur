// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Depth coverage tests for the internal SystemTextJsonSerializer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
public sealed partial class SystemTextJsonSerializerDepthShould
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsAccessorIsNull()
	{
		// Activator.CreateInstance wraps the inner exception in TargetInvocationException
		var ex = Should.Throw<TargetInvocationException>(() =>
		{
			Activator.CreateInstance(
				GetSerializerType(),
				new object?[] { null });
		});

		ex.InnerException.ShouldBeOfType<ArgumentNullException>();
	}

	[Fact]
	public void Resolve_ViaAddJsonSerialization_ReturnsSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();

		// Act
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithWebDefaults_UsesCamelCase()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

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
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act
		var result = serializer.Deserialize("{\"name\":\"hello\",\"count\":5}", typeof(TestPayload));

		// Assert
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Name.ShouldBe("hello");
		payload.Count.ShouldBe(5);
	}

	[Fact]
	public void Serialize_NullValue_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Deserialize_NullJson_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Deserialize(null!, typeof(TestPayload)));
	}

	[Fact]
	public void Serialize_NullType_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize(new TestPayload(), null!));
	}

	[Fact]
	public void Deserialize_NullType_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Deserialize("{}", null!));
	}

	[Fact]
	public void Serialize_WithIndentedOption_ProducesIndentedJson()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization(o => o.SerializerOptions.WriteIndented = true);
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act
		var json = serializer.Serialize(new TestPayload { Name = "indented" }, typeof(TestPayload));

		// Assert
		json.ShouldContain("\n");
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsJsonException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();

		// Act & Assert
		Should.Throw<JsonException>(() => serializer.Deserialize("{not valid}", typeof(TestPayload)));
	}

	[Fact]
	public void Roundtrip_PreservesData()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();
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
	public void SerializeAndDeserialize_UseSerializerContext_WhenConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization(o => o.SerializerContext = DictionaryStringStringJsonContext.Default);
		using var sp = services.BuildServiceProvider();
		var serializer = sp.GetRequiredService<IJsonSerializer>();
		var payload = new Dictionary<string, string> { ["key"] = "value" };

		// Act
		var json = serializer.Serialize(payload, typeof(Dictionary<string, string>));
		var result = serializer.Deserialize(json, typeof(Dictionary<string, string>));

		// Assert
		json.ShouldContain("\"key\":\"value\"");
		var roundTrip = result.ShouldBeOfType<Dictionary<string, string>>();
		roundTrip["key"].ShouldBe("value");
	}

	private static Type GetSerializerType()
	{
		var assembly = typeof(DispatchPatternsJsonOptions).Assembly;
		var type = assembly.GetType("Excalibur.Dispatch.Patterns.SystemTextJsonSerializer");
		type.ShouldNotBeNull();
		return type;
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
