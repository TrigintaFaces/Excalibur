// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Shouldly;

namespace Excalibur.Dispatch.Tests.EventSourcing;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Pattern", "Serialization")]
public sealed class EventMetadataResolverShould
{
	[Fact]
	public void SerializeDeclaredValuesAndExplicitNullWithoutReflectionFallback()
	{
		var metadata = new Dictionary<string, object>
		{
			["Text"] = "value",
			["Number"] = 42,
			["Null"] = null!,
			["Nested"] = new MetadataDeclaredValue { Name = "nested", Values = ["one", null] },
			["Envelope"] = new MetadataDeclaredEnvelope { Value = "declared" },
		};
		var bytes = EventSerializationDefaults.SerializeMetadataWithResolver(metadata, CreateOptions());
		using var document = JsonDocument.Parse(bytes);
		var root = document.RootElement;
		root.GetProperty("Text").GetString().ShouldBe("value");
		root.GetProperty("Number").GetInt32().ShouldBe(42);
		root.GetProperty("Null").ValueKind.ShouldBe(JsonValueKind.Null);
		root.GetProperty("Nested").GetProperty("name").GetString().ShouldBe("nested");
		root.GetProperty("Nested").GetProperty("values")[1].ValueKind.ShouldBe(JsonValueKind.Null);
		root.GetProperty("Envelope").GetProperty("value").GetString().ShouldBe("declared");
	}

	[Fact]
	public void RejectAnUndeclaredMetadataValueEvenWhenHostReflectionIsAvailable()
	{
		var metadata = new Dictionary<string, object> { ["Unknown"] = new MetadataUndeclaredValue() };
		var exception = Should.Throw<NotSupportedException>(() =>
			EventSerializationDefaults.SerializeMetadataWithResolver(metadata, CreateOptions()));
		exception.Message.ShouldContain(nameof(MetadataUndeclaredValue));
	}

	[Fact]
	public void RejectAnUndeclaredRuntimeValueInsideADeclaredMetadataEnvelope()
	{
		var metadata = new Dictionary<string, object>
		{
			["Envelope"] = new MetadataDeclaredEnvelope { Value = new MetadataUndeclaredValue() },
		};
		Should.Throw<NotSupportedException>(() =>
			EventSerializationDefaults.SerializeMetadataWithResolver(metadata, CreateOptions()));
	}

	private static JsonSerializerOptions CreateOptions()
	{
		var options = EventSerializationDefaults.CreateCanonicalOptions();
		EventSerializationDefaults.TryApplyTypeInfoResolver(options, MetadataResolverContext.Default).ShouldBeTrue();
		return options;
	}
}

internal sealed class MetadataDeclaredValue
{
	public string? Name { get; set; }
	public List<string?> Values { get; set; } = [];
}

internal sealed class MetadataDeclaredEnvelope
{
	public object? Value { get; set; }
}

internal sealed class MetadataUndeclaredValue
{
	public string Name { get; set; } = "undeclared";
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(MetadataDeclaredValue))]
[JsonSerializable(typeof(MetadataDeclaredEnvelope))]
internal sealed partial class MetadataResolverContext : JsonSerializerContext;
