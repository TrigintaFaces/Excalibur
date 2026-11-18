// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Dispatch.Serialization;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Pattern", "STRATEGY")]
public class AotJsonParsingContractShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadCompletePayloadWithWhitespace(bool array)
    {
        using var serializer = CreateSerializer(ParsingContractContext.Default);
        Read<ParsingContractPayload>(serializer, " \n{\"Name\":\"payload\"} \r\n", array).Name.ShouldBe("payload");
        Read<int[]>(serializer, " [1,2,3] ", array).ShouldBe([1, 2, 3]);
        Read<int>(serializer, " 42 ", array).ShouldBe(42);
    }

    [Theory]
    [InlineData("{\"Name\":\"payload\"} {}", false)]
    [InlineData("{\"Name\":\"payload\"} {}", true)]
    [InlineData("{\"Name\":\"payload\"} garbage", false)]
    [InlineData("{\"Name\":\"payload\"} garbage", true)]
    [InlineData("{\"Name\":", false)]
    [InlineData("{\"Name\":", true)]
    [InlineData("null {}", false)]
    [InlineData("null {}", true)]
    public void RejectIncompleteOrAdditionalInput(string json, bool array)
    {
        using var serializer = CreateSerializer(ParsingContractContext.Default);
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(serializer, json, array)).InnerException.ShouldBeOfType<JsonException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HonorTrailingCommaOption(bool array)
    {
        const string json = "{\"Name\":\"payload\",}";
        using var strict = CreateSerializer(ParsingContractContext.Default);
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(strict, json, array)).InnerException.ShouldBeOfType<JsonException>();
        using var allowed = CreateSerializer(new ParsingContractContext(new JsonSerializerOptions { AllowTrailingCommas = true }));
        Read<ParsingContractPayload>(allowed, json, array).Name.ShouldBe("payload");
    }

    [Theory]
    [InlineData("/*before*/{\"Name\":\"payload\"}", false)]
    [InlineData("/*before*/{\"Name\":\"payload\"}", true)]
    [InlineData("{\"Name\":/*inside*/\"payload\"}", false)]
    [InlineData("{\"Name\":/*inside*/\"payload\"}", true)]
    [InlineData("{\"Name\":\"payload\"}/*after*/", false)]
    [InlineData("{\"Name\":\"payload\"}/*after*/", true)]
    public void HonorCommentOption(string json, bool array)
    {
        using var strict = CreateSerializer(ParsingContractContext.Default);
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(strict, json, array)).InnerException.ShouldBeOfType<JsonException>();
        using var allowed = CreateSerializer(new ParsingContractContext(new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip }));
        Read<ParsingContractPayload>(allowed, json, array).Name.ShouldBe("payload");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HonorDepthBelowAndAboveDefault(bool array)
    {
        using var shallow = CreateSerializer(new ParsingContractContext(new JsonSerializerOptions { MaxDepth = 1 }));
        Should.Throw<SerializationException>(() => Read<ParsingContractNode>(shallow, "{\"Child\":{\"Child\":null}}", array)).InnerException.ShouldBeOfType<JsonException>();
        using var deep = CreateSerializer(new ParsingContractContext(new JsonSerializerOptions { MaxDepth = 128 }));
        string json = string.Concat(Enumerable.Repeat("{\"Child\":", 70)) + "null" + new string('}', 70);
        ParsingContractNode? node = Read<ParsingContractNode>(deep, json, array);
        for (int i = 0; i < 70; i++) { node.ShouldNotBeNull();node = node.Child; }
        node.ShouldBeNull();
        using var boundary = CreateSerializer(new ParsingContractContext(new JsonSerializerOptions { MaxDepth = 2 }));
        Read<ParsingContractNode>(boundary, "{\"Child\":{\"Child\":null}}", array).Child.ShouldNotBeNull();
        Should.Throw<SerializationException>(() => Read<ParsingContractNode>(boundary, "{\"Child\":{\"Child\":{\"Child\":null}}}", array)).InnerException.ShouldBeOfType<JsonException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveNullResultAndMissingMetadataErrors(bool array)
    {
        using var serializer = CreateSerializer(ParsingContractContext.Default);
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(serializer, "null", array)).Message.ShouldContain("returned null");
        Should.Throw<SerializationException>(() => Read<int?>(serializer, "null", array)).Message.ShouldContain("returned null");
        Should.Throw<SerializationException>(() => Read<UnregisteredParsingPayload>(serializer, "{}", array)).InnerException.ShouldBeOfType<NotSupportedException>();
    }

    [Fact]
    public void PreserveEmptyArgumentExceptions()
    {
        using var serializer = CreateSerializer(ParsingContractContext.Default);
        // Retain the historical case identity and all inputs. The canonical span-based codec
        // treats a null array as empty input and wraps the original JSON refusal for every form.
        Should.Throw<SerializationException>(() => serializer.Deserialize<ParsingContractPayload>((byte[])null!)).InnerException.ShouldBeOfType<JsonException>();
        Should.Throw<SerializationException>(() => serializer.Deserialize<ParsingContractPayload>(Array.Empty<byte>())).InnerException.ShouldBeOfType<JsonException>();
        Should.Throw<SerializationException>(() => serializer.Deserialize<ParsingContractPayload>(ReadOnlySpan<byte>.Empty)).InnerException.ShouldBeOfType<JsonException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveConverterOptionsResultsAndExceptionIdentity(bool array)
    {
        var converter = new ParsingContractConverter();
        var options = new JsonSerializerOptions();options.Converters.Add(converter);
        var context = new ParsingContractContext(options);
        using var serializer = CreateSerializer(context);
        Read<ParsingContractPayload>(serializer, "  {\"Name\":\"converted\"}", array).Name.ShouldBe("converted");
        var effectiveOptions = converter.Options.ShouldNotBeNull();
        effectiveOptions.IsReadOnly.ShouldBeTrue();
        if (!UseOwnedPolicy) { effectiveOptions.ShouldBeSameAs(context.GetTypeInfo(typeof(ParsingContractPayload))!.Options); }
        converter.TokenStart.ShouldBe(2);
        converter.ReturnNull = true;
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(serializer, "{}", array)).Message.ShouldContain("returned null");
        var sentinel = new InvalidOperationException("converter sentinel");converter.Failure = sentinel;
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(serializer, "{}", array)).InnerException.ShouldBeSameAs(sentinel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UseStandardSpanConverterValidationOrder(bool array)
    {
        var converter = new ParsingContractConverter();
        var options = new JsonSerializerOptions();options.Converters.Add(converter);
        using var serializer = CreateSerializer(new ParsingContractContext(options));
        Should.Throw<SerializationException>(() => Read<ParsingContractPayload>(serializer, "{\"Name\":\"converted\",\"Other\":[}", array)).InnerException.ShouldBeOfType<JsonException>();
        converter.Calls.ShouldBe(1);
    }

    protected virtual bool UseOwnedPolicy => false;
    private ParsingSerializer CreateSerializer(JsonSerializerContext context) => new(UseOwnedPolicy, context);
    private static T Read<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ParsingSerializer serializer, string json, bool array)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return array ? serializer.Deserialize<T>(bytes) : serializer.Deserialize<T>(bytes.AsSpan());
    }
}

public sealed class CompositeAotJsonParsingContractShould : AotJsonParsingContractShould
{
    protected override bool UseOwnedPolicy => true;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectFirstSupportingContextAndKeepItsOptionsAfterWarmup(bool array)
    {
        var permissive = new ParsingContractContext(new JsonSerializerOptions { AllowTrailingCommas = true });
        var serializer = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(permissive.Options)
        { TypeInfoResolver = JsonTypeInfoResolver.Combine(OtherParsingContext.Default, permissive, ParsingContractContext.Default) });
        byte[] bytes = "{\"Name\":\"priority\",}"u8.ToArray();
        for (int i = 0; i < 2; i++)
        {
            var result = array ? serializer.Deserialize<ParsingContractPayload>(bytes) : serializer.Deserialize<ParsingContractPayload>(bytes.AsSpan());
            result.Name.ShouldBe("priority");
            byte[] other = "\"other-context\""u8.ToArray();
            (array ? serializer.Deserialize<string>(other) : serializer.Deserialize<string>(other.AsSpan())).ShouldBe("other-context");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeverRetryLaterContextAfterSelectedParsingFailure(bool array)
    {
        var permissive = new ParsingContractContext(new JsonSerializerOptions { AllowTrailingCommas = true });
        var serializer = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(ParsingContractContext.Default.Options)
        { TypeInfoResolver = JsonTypeInfoResolver.Combine(ParsingContractContext.Default, permissive) });
        byte[] valid = "{\"Name\":\"warm\"}"u8.ToArray();
        byte[] trailingComma = "{\"Name\":\"priority\",}"u8.ToArray();
        for (int i = 0; i < 2; i++)
        {
            Should.Throw<SerializationException>(() => array ? serializer.Deserialize<ParsingContractPayload>(trailingComma) : serializer.Deserialize<ParsingContractPayload>(trailingComma.AsSpan())).InnerException.ShouldBeOfType<JsonException>();
            (array ? serializer.Deserialize<ParsingContractPayload>(valid) : serializer.Deserialize<ParsingContractPayload>(valid.AsSpan())).Name.ShouldBe("warm");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeverRetryLaterConverterAndPreserveItsException(bool array)
    {
        var first = new ParsingContractConverter();var second = new ParsingContractConverter();
        var firstOptions = new JsonSerializerOptions();firstOptions.Converters.Add(first);
        var secondOptions = new JsonSerializerOptions();secondOptions.Converters.Add(second);
        var serializer = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(firstOptions)
        { TypeInfoResolver = JsonTypeInfoResolver.Combine(new ParsingContractContext(firstOptions), new ParsingContractContext(secondOptions)) });
        byte[] bytes = "{\"Name\":\"first\"}"u8.ToArray();
        (array ? serializer.Deserialize<ParsingContractPayload>(bytes) : serializer.Deserialize<ParsingContractPayload>(bytes.AsSpan())).Name.ShouldBe("first");
        first.Calls.ShouldBe(1);second.Calls.ShouldBe(0);
        var sentinel = new InvalidOperationException("first context converter");first.Failure = sentinel;
        Should.Throw<SerializationException>(() => array ? serializer.Deserialize<ParsingContractPayload>(bytes) : serializer.Deserialize<ParsingContractPayload>(bytes.AsSpan())).InnerException.ShouldBeSameAs(sentinel);
        first.Calls.ShouldBe(2);second.Calls.ShouldBe(0);
    }
}

internal sealed class ParsingSerializer : IDisposable
{
    private readonly ISerializer _serializer;

    internal ParsingSerializer(bool useOwnedPolicy, JsonSerializerContext context)
    {
        _serializer = useOwnedPolicy ? SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(context.Options)
        { TypeInfoResolver = context }) : SystemTextJsonSerializer.Create(context);
    }

    internal T Deserialize<T>(byte[] data) => _serializer.Deserialize<T>(data);
    internal T Deserialize<T>(ReadOnlySpan<byte> data) => _serializer.Deserialize<T>(data);
    public void Dispose() { } // Test handle owns no resource; errors pass through unchanged.
}

public sealed class ParsingContractPayload
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ParsingContractNode
{
    public ParsingContractNode? Child { get; set; }
}

public sealed class UnregisteredParsingPayload;

internal sealed class ParsingContractConverter : JsonConverter<ParsingContractPayload>
{
    internal JsonSerializerOptions? Options { get; private set; }
    internal long TokenStart { get; private set; }
    internal int Calls { get; private set; }
    internal bool ReturnNull { get; set; }
    internal Exception? Failure { get; set; }

    public override ParsingContractPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Calls++;Options = options;TokenStart = reader.TokenStartIndex;
        if (Failure is not null) { throw Failure; }
        using var document = JsonDocument.ParseValue(ref reader);
        return ReturnNull ? null : new ParsingContractPayload { Name = document.RootElement.GetProperty("Name").GetString()! };
    }

    public override void Write(Utf8JsonWriter writer, ParsingContractPayload value, JsonSerializerOptions options) => throw new NotSupportedException();
}

[JsonSerializable(typeof(ParsingContractPayload))]
[JsonSerializable(typeof(ParsingContractNode))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(int[]))]
internal sealed partial class ParsingContractContext : JsonSerializerContext;

[JsonSerializable(typeof(string))]
internal sealed partial class OtherParsingContext : JsonSerializerContext;

