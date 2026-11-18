// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Pattern", "Regression")]
public sealed class AotSerializationRegistrationContractShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveApplicationAndCloudEventMetadataInEitherRegistrationOrder(bool cloudEventsFirst)
    {
        var options = Policy(RegistrationApplicationContext.Default);
        var codec = SystemTextJsonSerializer.CreateWithMetadata(options);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddDispatch(builder =>
        {
            if (cloudEventsFirst)
            {
                _ = builder.AddCloudEvents(RegistrationApplicationContext.Default);
            }

            _ = builder.WithSerialization(serialization => serialization
                .Register(codec, SerializerIds.SystemTextJson).UseCurrent(codec.Name));
            if (!cloudEventsFirst)
            {
                _ = builder.AddCloudEvents(RegistrationApplicationContext.Default);
            }
        });
        using var provider = services.BuildServiceProvider();
        var selected = provider.GetRequiredService<ISerializerRegistry>().GetCurrent();
        selected.Serializer.ShouldBeSameAs(codec);
        selected.Id.ShouldBe(SerializerIds.SystemTextJson);
        var payload = new RegistrationApplicationPayload("order-17", 23);
        var applicationBytes = codec.SerializeToBytes(payload);
        codec.Deserialize<RegistrationApplicationPayload>(applicationBytes).ShouldBe(payload);
        var envelope = new CloudEvent
        {
            Id = "event-17",
            Source = new Uri("urn:registration-contract"),
            Type = "contoso.order.accepted",
            Data = payload,
        };
        var frameworkValue = new Uri("https://orders.example/registration");
        codec.Deserialize<Uri>(codec.SerializeToBytes(frameworkValue)).ShouldBe(frameworkValue);
        var cloudEvents = provider.GetRequiredService<CloudEventSerializer>();
        var encoded = cloudEvents.Formatter.EncodeStructuredModeMessage(envelope, out _);
        using var document = JsonDocument.Parse(encoded);
        document.RootElement.GetProperty("id").GetString().ShouldBe("event-17");
        var expectedData = JsonSerializer.SerializeToElement(payload,
            (JsonTypeInfo<RegistrationApplicationPayload>)cloudEvents.Options.GetTypeInfo(typeof(RegistrationApplicationPayload)));
        document.RootElement.GetProperty("data").GetRawText().ShouldBe(expectedData.GetRawText());
    }

    [Fact]
    public void PreserveCompositeContributionsAndCoreMetadataThroughBothRegisteredFacades()
    {
        var codec = SystemTextJsonSerializer.CreateWithMetadata(Policy(
            JsonTypeInfoResolver.Combine(RegistrationApplicationContext.Default, RegistrationAdditionalContext.Default)));
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddPluggableSerializer(SerializerIds.SystemTextJson, codec, setAsCurrent: true);
        using var provider = services.BuildServiceProvider();
        var selected = provider.GetRequiredService<ISerializerRegistry>().GetCurrent().Serializer;
        var payloads = provider.GetRequiredService<IPayloadSerializer>();
        selected.ShouldBeSameAs(codec);
        var application = new RegistrationApplicationPayload("order-18", 24);
        var additional = new RegistrationAdditionalPayload("audit-18");
        var core = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "core-18" };

        selected.Deserialize<RegistrationApplicationPayload>(selected.SerializeToBytes(application)).ShouldBe(application);
        selected.Deserialize<RegistrationAdditionalPayload>(selected.SerializeToBytes(additional)).ShouldBe(additional);
        payloads.Deserialize<RegistrationApplicationPayload>(payloads.Serialize(application)).ShouldBe(application);
        payloads.Deserialize<RegistrationAdditionalPayload>(payloads.Serialize(additional)).ShouldBe(additional);
        selected.Deserialize<Dictionary<string, string>>(selected.SerializeToBytes(core))["message"].ShouldBe("core-18");
        payloads.Deserialize<Dictionary<string, string>>(payloads.Serialize(core))["message"].ShouldBe("core-18");
        var framed = payloads.Serialize(application);
        framed[0].ShouldBe(SerializerIds.SystemTextJson);
        framed.AsSpan(1).SequenceEqual(codec.SerializeToBytes(application)).ShouldBeTrue();
    }

    [Fact]
    public void KeepIndependentProvidersIsolatedAndRefuseUnregisteredPayloads()
    {
        var first = SystemTextJsonSerializer.CreateWithMetadata(Policy(RegistrationApplicationContext.Default));
        var second = SystemTextJsonSerializer.CreateWithMetadata(Policy(RegistrationAdditionalContext.Default));
        var firstServices = new ServiceCollection();
        _ = firstServices.AddPluggableSerializer(SerializerIds.SystemTextJson, first, setAsCurrent: true);
        var secondServices = new ServiceCollection();
        _ = secondServices.AddPluggableSerializer(SerializerIds.SystemTextJson, second, setAsCurrent: true);
        using var secondProvider = secondServices.BuildServiceProvider();
        secondProvider.GetRequiredService<ISerializerRegistry>().GetCurrent().Serializer.ShouldBeSameAs(second);
        var additional = new RegistrationAdditionalPayload("second-provider");
        using (var firstProvider = firstServices.BuildServiceProvider())
        {
            firstProvider.GetRequiredService<ISerializerRegistry>().GetCurrent().Serializer.ShouldBeSameAs(first);
            var application = new RegistrationApplicationPayload("first-provider", 25);
            first.Deserialize<RegistrationApplicationPayload>(first.SerializeToBytes(application)).ShouldBe(application);
            Should.Throw<SerializationException>(() => first.SerializeToBytes(additional)).InnerException
                .ShouldBeOfType<NotSupportedException>().Message.ShouldContain(nameof(RegistrationAdditionalPayload));
            Should.Throw<SerializationException>(() => second.SerializeToBytes(application)).InnerException
                .ShouldBeOfType<NotSupportedException>().Message.ShouldContain(nameof(RegistrationApplicationPayload));
        }

        second.Deserialize<RegistrationAdditionalPayload>(second.SerializeToBytes(additional)).ShouldBe(additional);
    }

    [Fact]
    public void ComposeANestedRuntimeGraphAndRefuseAnUndeclaredChild()
    {
        var child = new RegistrationAdditionalPayload("nested-child");
        var root = new RegistrationObjectPayload(child);
        var policy = Policy(JsonTypeInfoResolver.Combine(RegistrationObjectContext.Default, RegistrationAdditionalContext.Default));
        var codec = SystemTextJsonSerializer.CreateWithMetadata(policy);
        var reference = new JsonSerializerOptions(policy);
        reference.MakeReadOnly();
        var bytes = codec.SerializeToBytes(root);
        bytes.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(root,
            (JsonTypeInfo<RegistrationObjectPayload>)reference.GetTypeInfo(typeof(RegistrationObjectPayload))));
        codec.Deserialize<RegistrationObjectPayload>(bytes).Data.ShouldBeOfType<JsonElement>()
            .GetProperty("auditId").GetString().ShouldBe("nested-child");
        var incomplete = SystemTextJsonSerializer.CreateWithMetadata(Policy(RegistrationObjectContext.Default));
        Should.Throw<SerializationException>(() => incomplete.SerializeToBytes(root)).InnerException
            .ShouldBeOfType<NotSupportedException>().Message.ShouldContain(nameof(RegistrationAdditionalPayload));
    }

    [Fact]
    public void SnapshotCallerOptionsAndResolverMembershipWithoutFreezingTheCaller()
    {
        var options = Policy(RegistrationApplicationContext.Default);
        options.TypeInfoResolverChain.Add(RegistrationAdditionalContext.Default);
        var beforeConverter = new PayloadMarkerConverter("before");
        var afterConverter = new PayloadMarkerConverter("after");
        options.Converters.Add(beforeConverter);
        var codec = SystemTextJsonSerializer.CreateWithMetadata(options);
        options.IsReadOnly.ShouldBeFalse();
        var value = new RegistrationApplicationPayload("snapshot", 26);
        var before = codec.SerializeToBytes(value);
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
        options.TypeInfoResolverChain.Clear();
        options.Converters.Clear();
        options.Converters.Add(afterConverter);
        codec.SerializeToBytes(value).ShouldBe(before);
        beforeConverter.Writes.ShouldBe(2);
        afterConverter.Writes.ShouldBe(0);
        codec.Deserialize<RegistrationAdditionalPayload>(codec.SerializeToBytes(new RegistrationAdditionalPayload("retained")))
            .AuditId.ShouldBe("retained");
        var later = SystemTextJsonSerializer.CreateWithMetadata(options);
        Should.Throw<SerializationException>(() => later.SerializeToBytes(new RegistrationAdditionalPayload("not-covered")))
            .InnerException.ShouldBeOfType<NotSupportedException>();
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void HonorOnePolicyAcrossGenericRuntimeAndBufferPaths(string newLine)
    {
        var options = Policy(RegistrationApplicationContext.Default);
        options.WriteIndented = true;
        options.IndentCharacter = '\t';
        options.IndentSize = 1;
        options.NewLine = newLine;
        var codec = SystemTextJsonSerializer.CreateWithMetadata(options);
        var value = new RegistrationApplicationPayload("format", 27);
        var referenceOptions = new JsonSerializerOptions(options);
        referenceOptions.MakeReadOnly();
        var expected = JsonSerializer.SerializeToUtf8Bytes(value,
            (JsonTypeInfo<RegistrationApplicationPayload>)referenceOptions.GetTypeInfo(typeof(RegistrationApplicationPayload)));
        codec.SerializeToBytes(value).ShouldBe(expected);
        codec.SerializeObject(value, typeof(RegistrationApplicationPayload)).ShouldBe(expected);
        var buffer = new ArrayBufferWriter<byte>();
        codec.Serialize(value, buffer);
        buffer.WrittenSpan.SequenceEqual(expected).ShouldBeTrue();
        codec.Deserialize<RegistrationApplicationPayload>(expected).ShouldBe(value);
        codec.DeserializeObject(expected, typeof(RegistrationApplicationPayload)).ShouldBe(value);
    }

    [Fact]
    public void KeepDuplicateRegistryIdsAsAnActionableRefusal()
    {
        var codec = SystemTextJsonSerializer.CreateWithMetadata(Policy(RegistrationApplicationContext.Default));
        var services = new ServiceCollection();
        _ = services.AddPluggableSerializer(SerializerIds.SystemTextJson, codec, setAsCurrent: true);
        _ = services.AddPluggableSerializer(SerializerIds.SystemTextJson, codec, setAsCurrent: true);
        using var provider = services.BuildServiceProvider();
        Should.Throw<ArgumentException>(() => provider.GetRequiredService<ISerializerRegistry>()).Message.ShouldContain("2");
    }

    [Fact]
    public void RefuseMissingPolicyAndPreserveARequiredMetadataLookupFaultBeforeConstruction()
    {
        Should.Throw<ArgumentNullException>(() => SystemTextJsonSerializer.CreateWithMetadata(null!)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentException>(() => SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions()))
            .ParamName.ShouldBe("options");
        var sentinel = new InvalidOperationException("required envelope metadata unavailable");
        var options = Policy(new FailingEnvelopeResolver(sentinel));
        Should.Throw<InvalidOperationException>(() => SystemTextJsonSerializer.CreateWithMetadata(options)).ShouldBeSameAs(sentinel);
    }

    [Fact]
    public void KeepGeneratedFrameworkFallbacksWhenTheConsumerResolverReturnsNoMetadata()
    {
        var codec = SystemTextJsonSerializer.CreateWithMetadata(Policy(JsonTypeInfoResolver.Combine()));
        var value = new Uri("https://orders.example/framework-fallback");
        codec.Deserialize<Uri>(codec.SerializeToBytes(value)).ShouldBe(value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefuseWrongTypeOrForeignOptionsMetadataBeforeReturningACodec(bool foreignOptions)
    {
        var options = Policy(new IncorrectEnvelopeResolver(foreignOptions));
        Should.Throw<InvalidOperationException>(() => SystemTextJsonSerializer.CreateWithMetadata(options));
    }

    private static JsonSerializerOptions Policy(IJsonTypeInfoResolver resolver) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = resolver,
    };

    private sealed class PayloadMarkerConverter(string marker) : JsonConverter<RegistrationApplicationPayload>
    {
        internal int Writes { get; private set; }

        public override RegistrationApplicationPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("This control observes serialization converter membership.");

        public override void Write(Utf8JsonWriter writer, RegistrationApplicationPayload value, JsonSerializerOptions options)
        {
            Writes++;
            writer.WriteStringValue(marker + ":" + value.OrderId);
        }
    }

    private sealed class IncorrectEnvelopeResolver(bool foreignOptions) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type != typeof(CloudEvent))
            {
                return null;
            }

            if (foreignOptions)
            {
                var foreign = new JsonSerializerOptions(options) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper };
                return ((IJsonTypeInfoResolver)CloudEventJsonContext.Default).GetTypeInfo(type, foreign);
            }

            return ((IJsonTypeInfoResolver)CloudEventJsonContext.Default).GetTypeInfo(typeof(Uri), options);
        }
    }

    private sealed class FailingEnvelopeResolver(InvalidOperationException failure) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => throw failure;
    }
}

internal sealed record RegistrationApplicationPayload(string OrderId, int Quantity);

internal sealed record RegistrationAdditionalPayload(string AuditId);

internal sealed record RegistrationObjectPayload(object Data);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RegistrationApplicationPayload))]
internal sealed partial class RegistrationApplicationContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RegistrationAdditionalPayload))]
internal sealed partial class RegistrationAdditionalContext : JsonSerializerContext;

[JsonSerializable(typeof(RegistrationObjectPayload))]
internal sealed partial class RegistrationObjectContext : JsonSerializerContext;
