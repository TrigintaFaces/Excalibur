// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Shouldly;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>Current public API controls; a forwarding resolver's valid child metadata must remain usable.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class EventMetadataForwardingResolverShould
{
	[Fact]
	public void SerializeRegisteredValueUsingForwardedChildContextMetadata()
	{
		var options = Options();
		options.TypeInfoResolver = new ForwardingResolver(ForwardedEventMetadataContext.Default);
		options.MakeReadOnly();
		var bytes = EventSerializationDefaults.SerializeMetadataWithResolver(
			new Dictionary<string, object> { ["integer"] = 7 }, options);
		System.Text.Encoding.UTF8.GetString(bytes).ShouldBe("{\"integer\":7}");
	}

	[Fact]
	public void PreserveBoundContextKeysAndExplicitNullDictionaryEntries()
	{
		var options = Options();
		_ = new ForwardedEventMetadataContext(options);
		var bytes = EventSerializationDefaults.SerializeMetadataWithResolver(
			new Dictionary<string, object> { ["MixedCaseKey"] = 7, ["null-entry"] = null! }, options);
		System.Text.Encoding.UTF8.GetString(bytes).ShouldBe("{\"MixedCaseKey\":7,\"null-entry\":null}");
		options.IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public void RejectMissingRuntimeMetadataWithoutFallingBackToReflection()
	{
		var options = Options();
		_ = new ForwardedEventMetadataContext(options);
		_ = Should.Throw<NotSupportedException>(() => EventSerializationDefaults.SerializeMetadataWithResolver(
			new Dictionary<string, object> { ["unregistered"] = new UnregisteredEventMetadataValue("hidden") }, options));
	}

	[Fact]
	public void RejectMetadataForTheWrongType()
	{
		var options = Options();
		options.TypeInfoResolver = new IncorrectTypeResolver();
		options.MakeReadOnly();
		_ = Should.Throw<InvalidOperationException>(() => EventSerializationDefaults.SerializeMetadataWithResolver(
			new Dictionary<string, object> { ["integer"] = 7 }, options));
	}

	private static JsonSerializerOptions Options() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private sealed class ForwardingResolver(JsonSerializerContext context) : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => context.GetTypeInfo(type);
	}

	private sealed class IncorrectTypeResolver : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => ForwardedEventMetadataContext.Default.String;
	}
}

internal sealed record UnregisteredEventMetadataValue(string Value);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal sealed partial class ForwardedEventMetadataContext : JsonSerializerContext
{
}
