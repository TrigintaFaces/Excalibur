// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
[Trait("Pattern", "Regression")]
public sealed class CloudEventRegisteredFormatterShould
{
	[Fact]
	public void EncodeStructuredAndBatchPayloadsThroughThePublicResolverRegistration()
	{
		var services = new ServiceCollection();
		_ = services.AddDispatch(builder => builder.AddCloudEvents(PayloadContext.Default));
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();
		var expected = new JsonEventFormatter(serializer.Options, default);
		var first = Event(new ConformancePayload { Name = "first", Count = 22 });
		var second = Event(new ConformancePayload { Name = "second", Count = 23 });
		second.Id = "second";

		serializer.Formatter.EncodeStructuredModeMessage(first, out var actualType).ToArray()
			.ShouldBe(expected.EncodeStructuredModeMessage(first, out var expectedType).ToArray());
		actualType.ToString().ShouldBe(expectedType.ToString());
		var batch = serializer.Formatter.EncodeBatchModeMessage([first, second], out var batchType);
		batch.ToArray().ShouldBe(expected.EncodeBatchModeMessage([first, second], out var expectedBatchType).ToArray());
		batchType.ToString().ShouldBe(expectedBatchType.ToString());
		var decoded = serializer.Formatter.DecodeBatchModeMessage(batch, batchType, extensionAttributes: null);
		decoded.Count.ShouldBe(2);
		decoded[0].Data.ShouldBeOfType<JsonElement>().GetProperty("Name").GetString().ShouldBe("first");
		decoded[1].Id.ShouldBe("second");
		serializer.Options.IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public void KeepTheSameOptionsAndLateConfiguredPayloadConverterAndWriterSettings()
	{
		var options = new JsonSerializerOptions(CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default))
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};
		var serializer = CloudEventSerializer.CreateWithMetadata(options);
		serializer.Options.ShouldBeSameAs(options);
		options.IsReadOnly.ShouldBeFalse();
		var converter = new PayloadConverter();
		options.Converters.Add(converter);
		var cloudEvent = Event(new ConformancePayload { Name = "<雪>", Count = 22 });
		var expected = new JsonEventFormatter(options, default).EncodeStructuredModeMessage(cloudEvent, out _);
		converter.Writes = 0;

		var actual = serializer.Formatter.EncodeStructuredModeMessage(cloudEvent, out _);

		actual.ToArray().ShouldBe(expected.ToArray());
		converter.Writes.ShouldBe(1);
		Encoding.UTF8.GetString(actual.Span).ShouldContain("<雪>");
		Encoding.UTF8.GetString(actual.Span).ShouldContain("\n");
		options.IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public void PreserveAnExplicitObjectRootConverterInsteadOfSwitchingToRuntimeTypeMetadata()
	{
		var options = new JsonSerializerOptions(CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default));
		var converter = new ObjectConverter();
		options.Converters.Add(converter);
		var serializer = CloudEventSerializer.CreateWithMetadata(options);
		var cloudEvent = Event(new ConformancePayload { Name = "not-the-root-converter", Count = 22 });
		var expected = new JsonEventFormatter(options, default).EncodeStructuredModeMessage(cloudEvent, out _);
		converter.Writes = 0;

		var actual = serializer.Formatter.EncodeStructuredModeMessage(cloudEvent, out _);

		actual.ToArray().ShouldBe(expected.ToArray());
		converter.Writes.ShouldBe(1);
		using var json = JsonDocument.Parse(actual);
		json.RootElement.GetProperty("data").GetString().ShouldBe("object-root");
	}

	[Fact]
	public void PreserveCoveredInterfaceAncestorSerializationWithoutExactRuntimeMetadata()
	{
		var options = CloudEventPayloadSerialization.CreateOptions(FormatterAncestorContext.Default);
		options.TryGetTypeInfo(typeof(PrivatePayload), out _).ShouldBeFalse();
		var cloudEvent = Event(new PrivatePayload { Name = "interface-payload" });
		var expected = new JsonEventFormatter(options, default).EncodeStructuredModeMessage(cloudEvent, out _);
		var serializer = CloudEventSerializer.CreateWithMetadata(options);

		serializer.Formatter.EncodeStructuredModeMessage(cloudEvent, out _).ToArray().ShouldBe(expected.ToArray());
	}

	[Fact]
	public void KeepTheSdkSerializationOnlyObjectContractWhenOnlyPayloadMetadataIsSupplied()
	{
		var resolver = new PayloadOnlyResolver();
		var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
		resolver.GetTypeInfo(typeof(object), options).ShouldBeNull();
		var cloudEvent = Event(new ConformancePayload { Name = "payload-only", Count = 22 });
		var expected = new JsonEventFormatter(options, default).EncodeStructuredModeMessage(cloudEvent, out _);
		var serializer = CloudEventSerializer.CreateWithMetadata(options);

		serializer.Formatter.EncodeStructuredModeMessage(cloudEvent, out _).ToArray().ShouldBe(expected.ToArray());
	}

	[Fact]
	public void RefuseUncoveredPayloadMetadataByNameAcrossBothDataHooks()
	{
		var services = new ServiceCollection();
		_ = services.AddDispatch(builder => builder.AddCloudEvents(PayloadContext.Default));
		using var provider = services.BuildServiceProvider();
		var formatter = provider.GetRequiredService<CloudEventSerializer>().Formatter;
		var cloudEvent = Event(new ConformanceUndeclared { Value = "not-covered" });

		Should.Throw<NotSupportedException>(() => formatter.EncodeStructuredModeMessage(cloudEvent, out _))
			.Message.ShouldContain(nameof(ConformanceUndeclared));
		Should.Throw<NotSupportedException>(() => formatter.EncodeBinaryModeEventData(cloudEvent))
			.Message.ShouldContain(nameof(ConformanceUndeclared));
		Should.Throw<NotSupportedException>(() => formatter.EncodeBatchModeMessage([cloudEvent], out _))
			.Message.ShouldContain(nameof(ConformanceUndeclared));
	}

	[Fact]
	public void KeepRawOptionsAndAnExplicitCustomFactoryOnTheLegacyConstructor()
	{
		var options = new JsonSerializerOptions { WriteIndented = true };
		options.TypeInfoResolver.ShouldBeNull();
		var services = new ServiceCollection();
		_ = services.AddDispatch(builder => builder.AddCloudEvents(PayloadContext.Default));
		services.AddSingleton<CloudEventSerializer>(_ => new CloudEventSerializer(options));
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<CloudEventSerializer>();
		serializer.Options.ShouldBeSameAs(options);
		serializer.Formatter.GetType().ShouldBe(typeof(JsonEventFormatter));
		var cloudEvent = Event(new ConformanceUndeclared { Value = "legacy-reflection" });
		var expected = new JsonEventFormatter(options, default).EncodeStructuredModeMessage(cloudEvent, out _);

		var actual = serializer.Formatter.EncodeStructuredModeMessage(cloudEvent, out _);

		actual.ToArray().ShouldBe(expected.ToArray());
		using var json = JsonDocument.Parse(actual);
		json.RootElement.GetProperty("data").GetProperty("Value").GetString().ShouldBe("legacy-reflection");
	}

	[Fact]
	public void RequireAnExplicitResolverAndRefuseAnEmptyPayloadResolver()
	{
		Should.Throw<ArgumentNullException>(() => CloudEventSerializer.CreateWithMetadata(null!));
		var missing = Should.Throw<ArgumentException>(() => CloudEventSerializer.CreateWithMetadata(new JsonSerializerOptions()));
		missing.Message.ShouldContain("TypeInfoResolver");
		var formatter = CloudEventSerializer.CreateWithMetadata(new JsonSerializerOptions
		{
			TypeInfoResolver = new EmptyResolver(),
		}).Formatter;
		Should.Throw<NotSupportedException>(() => formatter.EncodeStructuredModeMessage(
			Event(new ConformancePayload { Name = "missing", Count = 22 }), out _)).Message.ShouldContain(nameof(ConformancePayload));
	}

	[Theory]
	[InlineData("application/json; charset=utf-8")]
	[InlineData("application/problem+json; charset=utf-16")]
	public void PreserveBinaryJsonCharsetAndPayloadRepresentation(string contentType)
	{
		var options = CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default);
		var cloudEvent = Event(new ConformancePayload { Name = "雪", Count = 22 });
		cloudEvent.DataContentType = contentType;
		var expected = new JsonEventFormatter(options, default).EncodeBinaryModeEventData(cloudEvent);
		var actual = CloudEventSerializer.CreateWithMetadata(options).Formatter.EncodeBinaryModeEventData(cloudEvent);

		actual.ToArray().ShouldBe(expected.ToArray());
	}

	[Fact]
	public void PreserveBinaryNullByteArrayAndNonJsonTextAndRefusalRules()
	{
		var options = CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default);
		var formatter = CloudEventSerializer.CreateWithMetadata(options).Formatter;
		formatter.EncodeBinaryModeEventData(Event(null)).Length.ShouldBe(0);
		var bytes = new byte[] { 1, 2, 255 };
		var binaryEvent = Event(bytes);
		binaryEvent.DataContentType = "application/json";
		var actual = formatter.EncodeBinaryModeEventData(binaryEvent);
		actual.ToArray().ShouldBe(bytes);
		bytes[0] = 7;
		actual.Span[0].ShouldBe((byte)7);
		var text = Event("雪");
		text.DataContentType = "text/plain; charset=utf-16";
		formatter.EncodeBinaryModeEventData(text).ToArray()
			.ShouldBe(new JsonEventFormatter(options, default).EncodeBinaryModeEventData(text).ToArray());
		var unsupported = Event(new ConformancePayload());
		unsupported.DataContentType = "application/octet-stream";
		Should.Throw<ArgumentException>(() => formatter.EncodeBinaryModeEventData(unsupported));
		Should.Throw<ArgumentException>(() => formatter.EncodeStructuredModeMessage(unsupported, out _));
		Should.Throw<ArgumentException>(() => formatter.EncodeBinaryModeEventData(new CloudEvent()));
	}

	private static CloudEvent Event(object? data) => new()
	{
		Id = "registered-formatter",
		Type = "excalibur.registered.formatter",
		Source = new Uri("https://example.test/registered"),
		Data = data,
	};

	private sealed class PayloadOnlyResolver : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) =>
			type == typeof(object) ? null : ((IJsonTypeInfoResolver)PayloadContext.Default).GetTypeInfo(type, options);
	}

	private sealed class EmptyResolver : IJsonTypeInfoResolver
	{
		public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
	}

	private sealed class PrivatePayload : IFormatterAncestorPayload
	{
		public string Name { get; init; } = string.Empty;
	}

	private sealed class PayloadConverter : JsonConverter<ConformancePayload>
	{
		public int Writes { get; set; }

		public override ConformancePayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException("This converter verifies outgoing payloads.");

		public override void Write(Utf8JsonWriter writer, ConformancePayload value, JsonSerializerOptions options)
		{
			Writes++;
			writer.WriteStringValue(value.Name);
		}
	}

	private sealed class ObjectConverter : JsonConverter<object>
	{
		public int Writes { get; set; }

		public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException("This converter verifies outgoing object-root dispatch.");

		public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			Writes++;
			writer.WriteStringValue("object-root");
		}
	}
}

internal interface IFormatterAncestorPayload
{
	string Name { get; }
}

[JsonSerializable(typeof(IFormatterAncestorPayload))]
internal sealed partial class FormatterAncestorContext : JsonSerializerContext;
