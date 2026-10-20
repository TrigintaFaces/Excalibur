// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class DispatchJsonSerializerMetadataShould
{
	[Fact]
	public async Task PreserveLazyConcurrentJitFallbackWhenTheConsumerClearsTheResolver()
	{
		JsonSerializerOptions? configured = null;
		using var serializer = new DispatchJsonSerializer(options =>
		{
			options.TypeInfoResolver = null;
			configured = options;
		});
		configured.ShouldNotBeNull().IsReadOnly.ShouldBeFalse();
		var operations = Enumerable.Range(0, 32).Select(index => Task.Run(() =>
		{
			var expected = new MetadataPayload(index.ToString(System.Globalization.CultureInfo.InvariantCulture), null);
			serializer.Deserialize<MetadataPayload>(serializer.Serialize(expected)).ShouldBe(expected);
		}));
		await Task.WhenAll(operations);
		configured.IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public async Task UseConfiguredMetadataAndFormattingAcrossPooledAndStreamPaths()
	{
		using var serializer = new DispatchJsonSerializer(options =>
		{
			options.TypeInfoResolver = MetadataTestJsonContext.Default;
			options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
			options.WriteIndented = true;
		}, MetadataTestJsonContext.Default);
		var value = new MetadataPayload("payload", null);
		var json = serializer.Serialize(value);
		json.ShouldContain("\n");
		json.ShouldContain("\"DISPLAY_NAME\"");
		json.ShouldNotContain("OPTIONAL_VALUE");
#pragma warning disable CA2263 // This regression compares the runtime-Type API with the generic API; changing overloads would remove its oracle.
		serializer.Serialize(value, typeof(MetadataPayload)).ShouldBe(json);
#pragma warning restore CA2263
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		(await serializer.DeserializeFromStreamAsync<MetadataPayload>(stream, CancellationToken.None)).ShouldBe(value);
		stream.Position = 0;
		(await serializer.DeserializeFromStreamAsync(stream, typeof(MetadataPayload), CancellationToken.None)).ShouldBe(value);
	}

	[Fact]
	public void HonorConsumerConvertersWhenResolvingConfiguredMetadata()
	{
		using var serializer = new DispatchJsonSerializer(options =>
		{
			options.TypeInfoResolver = MetadataTestJsonContext.Default;
			options.Converters.Add(new MetadataPayloadConverter());
		}, MetadataTestJsonContext.Default);
		var payload = new MetadataPayload("custom", null);
		serializer.Serialize(payload).ShouldBe("\"custom\"");
		serializer.Deserialize<MetadataPayload>("\"custom\"").ShouldBe(payload);
	}

	[Fact]
	public void PreserveTheDeclaredBaseContractRatherThanTheDerivedRuntimeShape()
	{
		using var serializer = new DispatchJsonSerializer(options => options.TypeInfoResolver = MetadataTestJsonContext.Default);
		MetadataBasePayload value = new MetadataDerivedPayload { BaseValue = "base", DerivedValue = "derived" };
		serializer.Serialize(value).ShouldBe("{\"baseValue\":\"base\"}");
	}

	private sealed class MetadataPayloadConverter : JsonConverter<MetadataPayload>
	{
		public override MetadataPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			new(reader.GetString()!, null);
		public override void Write(Utf8JsonWriter writer, MetadataPayload value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.DisplayName);
	}
}

public sealed record MetadataPayload(string DisplayName, string? OptionalValue);
public class MetadataBasePayload
{
	public string BaseValue { get; init; } = string.Empty;
}
public sealed class MetadataDerivedPayload : MetadataBasePayload
{
	public string DerivedValue { get; init; } = string.Empty;
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MetadataPayload))]
[JsonSerializable(typeof(MetadataBasePayload))]
internal sealed partial class MetadataTestJsonContext : JsonSerializerContext;
