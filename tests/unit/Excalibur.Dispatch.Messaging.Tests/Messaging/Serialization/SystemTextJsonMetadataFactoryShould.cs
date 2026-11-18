// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Serialization")]
public sealed class SystemTextJsonMetadataFactoryShould
{
    [Fact]
    public void MatchGeneratedMetadataAcrossGenericBufferRuntimeBytesAndUtf8Reads()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var value = new FactoryCodecDto { Name = "<native>", Count = 7 };
        var metadata = (JsonTypeInfo<FactoryCodecDto>)FactoryCodecContext.Default.GetTypeInfo(typeof(FactoryCodecDto))!;
        var expected = JsonSerializer.SerializeToUtf8Bytes(value, metadata);
        var buffer = new ArrayBufferWriter<byte>();
        codec.Serialize(value, buffer);
        buffer.WrittenSpan.ToArray().ShouldBe(expected);
        codec.SerializeObject(value, typeof(FactoryCodecDto)).ShouldBe(expected);
        codec.Deserialize<FactoryCodecDto>(buffer.WrittenSpan).Name.ShouldBe(value.Name);
        codec.DeserializeObject(expected, typeof(FactoryCodecDto)).ShouldBeOfType<FactoryCodecDto>().Count.ShouldBe(7);
        codec.Deserialize<FactoryCodecDto>(new ReadOnlyMemory<byte>(expected)).Name.ShouldBe(value.Name);
        codec.Deserialize<FactoryCodecDto>(new ReadOnlySequence<byte>(expected)).Count.ShouldBe(7);
        codec.DeserializeFromString<FactoryCodecDto>(codec.SerializeToString(value)).Count.ShouldBe(7);
    }

    [Fact]
    public void HonorForwardedStaticMetadataAndItsOwnEffectiveWriterOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var child = new FactoryCodecContext(options);
        var forwarding = new ForwardingContext(child);
        ReferenceEquals(child.Options, forwarding.Options).ShouldBeFalse();
        forwarding.Options.WriteIndented.ShouldBeFalse();
        var codec = SystemTextJsonSerializer.Create(forwarding);
        var value = new FactoryCodecDto { Name = "<forwarded>", Count = 9 };
        var expected = JsonSerializer.SerializeToUtf8Bytes(value, (JsonTypeInfo<FactoryCodecDto>)child.GetTypeInfo(typeof(FactoryCodecDto))!);
        var actual = codec.SerializeToBytes(value);
        actual.ShouldBe(expected);
        Encoding.UTF8.GetString(actual).ShouldContain("<forwarded>");
        Encoding.UTF8.GetString(actual).ShouldContain('\n');
        codec.Deserialize<FactoryCodecDto>(actual).Count.ShouldBe(9);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PreserveEffectiveIndentationAndNewLineAcrossGenericRuntimeAndDirectMetadata(string newLine)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            IndentCharacter = '\t',
            IndentSize = 1,
            NewLine = newLine,
        };
        var context = new FactoryCodecContext(options);
        var codec = SystemTextJsonSerializer.Create(context);
        var value = new FactoryCodecDto { Name = "formatted", Count = 23 };
        var metadata = (JsonTypeInfo<FactoryCodecDto>)context.GetTypeInfo(typeof(FactoryCodecDto))!;
        var expected = JsonSerializer.SerializeToUtf8Bytes(value, metadata);
        Encoding.UTF8.GetString(expected).ShouldContain(newLine + "\t\"name\"");
        codec.SerializeObject(value, typeof(FactoryCodecDto)).ShouldBe(expected);

        var buffer = new ArrayBufferWriter<byte>();
        codec.Serialize(value, buffer);
        buffer.WrittenSpan.ToArray().ShouldBe(expected);
    }

    [Fact]
    public void FailMissingRootAndRuntimeObjectMetadataEvenWhenManagedReflectionIsAvailable()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var unknown = new MissingCodecDto { Text = "not registered" };
        Should.Throw<SerializationException>(() => codec.SerializeToBytes(unknown))
            .InnerException.ShouldBeOfType<NotSupportedException>();
        Should.Throw<SerializationException>(() => codec.SerializeObject(unknown, typeof(MissingCodecDto)))
            .InnerException.ShouldBeOfType<NotSupportedException>();
        Should.Throw<SerializationException>(() => codec.DeserializeObject("{}"u8, typeof(MissingCodecDto)))
            .InnerException.ShouldBeOfType<NotSupportedException>();
        // object itself IS registered, so this reaches the runtime concrete-type boundary.
        Should.Throw<SerializationException>(() => codec.SerializeToBytes<object>(unknown))
            .InnerException.ShouldBeOfType<NotSupportedException>();
    }

    [Fact]
    public void PreserveDeclaredGenericTypeInsteadOfSilentlySwitchingToRuntimeType()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        FactoryCodecBase value = new FactoryCodecDerived { Name = "base", Extra = "not-declared" };
        var json = codec.SerializeToString(value);
        json.ShouldContain("base");
        json.ShouldNotContain("not-declared");
        json.ShouldNotContain("extra");
    }

    [Fact]
    public void PreserveArgumentErrorForIncompatibleRuntimeValues()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var error = Should.Throw<SerializationException>(() =>
            codec.SerializeObject(new FactoryCodecDto(), typeof(FactoryCodecBase)));
        error.InnerException.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("value");
    }

    [Fact]
    public void AcceptAssignableBaseInterfaceAndBoxedNullableValues()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var value = new FactoryCodecDerived { Name = "declared", Extra = "runtime-only" };
        var baseJson = Encoding.UTF8.GetString(codec.SerializeObject(value, typeof(FactoryCodecBase)));
        baseJson.ShouldContain("declared");
        baseJson.ShouldNotContain("runtime-only");
        var interfaceJson = Encoding.UTF8.GetString(codec.SerializeObject(value, typeof(IFactoryCodecNamed)));
        interfaceJson.ShouldContain("declared");
        interfaceJson.ShouldNotContain("runtime-only");
        object boxedNullable = (int?)17;
        Encoding.UTF8.GetString(codec.SerializeObject(boxedNullable, typeof(int?))).ShouldBe("17");
        Encoding.UTF8.GetString(codec.SerializeObject(boxedNullable, typeof(int))).ShouldBe("17");
    }

    [Fact]
    public void PreserveNullArgumentMalformedJsonAndNullResultContracts()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var buffer = new ArrayBufferWriter<byte>();
        Should.Throw<ArgumentNullException>(() => SystemTextJsonSerializer.Create(null!));
        Should.Throw<ArgumentNullException>(() => codec.Serialize<FactoryCodecDto>(null!, buffer));
        Should.Throw<ArgumentNullException>(() => codec.Serialize(new FactoryCodecDto(), null!));
        Should.Throw<ArgumentNullException>(() => codec.SerializeObject(null!, typeof(FactoryCodecDto)));
        Should.Throw<ArgumentNullException>(() => codec.SerializeObject(new FactoryCodecDto(), null!));
        Should.Throw<ArgumentNullException>(() => codec.DeserializeObject("{}"u8, null!));
        Should.Throw<SerializationException>(() => codec.Deserialize<FactoryCodecDto>("{"u8))
            .InnerException.ShouldBeOfType<JsonException>();
        Should.Throw<SerializationException>(() => codec.Deserialize<FactoryCodecDto>("null"u8));
        Should.Throw<SerializationException>(() => codec.DeserializeObject("null"u8, typeof(FactoryCodecDto)));
    }

    [Fact]
    public void PreserveConvertersNumberReferenceNamingEncoderAndReadDepthOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
            ReferenceHandler = ReferenceHandler.Preserve,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            MaxDepth = 8,
        };
        options.Converters.Add(new JsonStringEnumConverter<FactoryCodecMode>());
        var context = new FactoryCodecContext(options);
        var codec = SystemTextJsonSerializer.Create(context);
        var value = new FactoryCodecDto { Name = "<configured>", Count = 7, Mode = FactoryCodecMode.Second };
        value.Next = value;
        var bytes = codec.SerializeToBytes(value);
        var expected = JsonSerializer.SerializeToUtf8Bytes(value, (JsonTypeInfo<FactoryCodecDto>)context.GetTypeInfo(typeof(FactoryCodecDto))!);
        bytes.ShouldBe(expected);
        var json = Encoding.UTF8.GetString(bytes);
        json.ShouldContain("\"count\": \"7\"");
        json.ShouldContain("Second");
        json.ShouldContain("<configured>");
        var restored = codec.Deserialize<FactoryCodecDto>(bytes);
        restored.Next.ShouldBeSameAs(restored);
        restored.Mode.ShouldBe(FactoryCodecMode.Second);
        var tooDeep = string.Concat(Enumerable.Repeat("{\"next\":", 9)) + "{}" + new string('}', 9);
        using var independentlyParsed = JsonDocument.Parse(tooDeep);
        independentlyParsed.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        context.Options.MaxDepth.ShouldBe(8);
        Should.Throw<SerializationException>(() => codec.DeserializeFromString<FactoryCodecDto>(tooDeep))
            .InnerException.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public void SnapshotMutableLegacyOptionsWithoutMutatingTheCallerAndKeepReadonlyContextOptionsUsable()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        var legacy = new SystemTextJsonSerializer(options);
        options.IsReadOnly.ShouldBeFalse();
        options.TypeInfoResolver.ShouldBeNull();
        options.PropertyNamingPolicy = null;
        options.WriteIndented = false;
        var json = legacy.SerializeToString(new FactoryCodecDto { Name = "snapshot", Count = 7 });
        json.ShouldContain("\"name\"");
        json.ShouldContain('\n');
        var frozen = new SystemTextJsonSerializer(FactoryCodecContext.Default.Options);
        var value = new FactoryCodecDto { Name = "immutable", Count = 8 };
        frozen.Deserialize<FactoryCodecDto>(frozen.SerializeToBytes(value)).Count.ShouldBe(8);
        // Existing nullable constructor call remains unambiguous and functional.
        var defaults = new SystemTextJsonSerializer(null);
        defaults.Deserialize<int>(defaults.SerializeToBytes(9)).ShouldBe(9);
    }

    [Fact]
    public async Task PreserveAsyncStreamAndCancellationBehaviorThroughTheSameCodec()
    {
        var codec = SystemTextJsonSerializer.Create(FactoryCodecContext.Default);
        var value = new FactoryCodecDto { Name = "stream", Count = 12 };
        await using var stream = new MemoryStream();
        await codec.SerializeAsync(stream, value, CancellationToken.None);
        stream.Position = 0;
        (await codec.DeserializeAsync<FactoryCodecDto>(stream, CancellationToken.None)).Count.ShouldBe(12);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var length = stream.Length;
        await Should.ThrowAsync<OperationCanceledException>(async () => await codec.SerializeAsync(stream, value, cancellation.Token));
        stream.Length.ShouldBe(length);
    }

    public enum FactoryCodecMode { First, Second }

    internal sealed class FactoryCodecDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public FactoryCodecMode Mode { get; set; }
        public FactoryCodecDto? Next { get; set; }
    }

    internal interface IFactoryCodecNamed { string Name { get; } }
    internal class FactoryCodecBase : IFactoryCodecNamed { public string Name { get; set; } = string.Empty; }
    internal sealed class FactoryCodecDerived : FactoryCodecBase { public string Extra { get; set; } = string.Empty; }
    internal sealed class MissingCodecDto { public string Text { get; set; } = string.Empty; }

    private sealed class ForwardingContext(JsonSerializerContext child) : JsonSerializerContext(options: null)
    {
        protected override JsonSerializerOptions? GeneratedSerializerOptions => null;
        public override JsonTypeInfo? GetTypeInfo(Type type) => child.GetTypeInfo(type);
    }
}

[JsonSerializable(typeof(SystemTextJsonMetadataFactoryShould.FactoryCodecDto))]
[JsonSerializable(typeof(SystemTextJsonMetadataFactoryShould.FactoryCodecBase))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(SystemTextJsonMetadataFactoryShould.IFactoryCodecNamed))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
internal sealed partial class FactoryCodecContext : JsonSerializerContext;
