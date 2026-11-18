// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AotJsonContractProbe;

internal static class Program
{
public static void Main()
{
if (RuntimeFeature.IsDynamicCodeSupported) throw new InvalidOperationException("dynamic code must be disabled; a native result requires running the published native executable");
int checks = 0;
void Require(bool condition)
{
    if (!condition) throw new InvalidOperationException("Native JSON contract failed at " + checks);
    checks++;
}
Require(!JsonSerializer.IsReflectionEnabledByDefault);
var serializer = SystemTextJsonSerializer.Create(ProbeContext.Default);
var message = new ProbePayload { Name = "native", Amount = 42.5m };
byte[] bytes = serializer.SerializeToBytes(message);
var restored = serializer.Deserialize<ProbePayload>(bytes);
Require(restored.Name == message.Name && restored.Amount == message.Amount);
restored = serializer.Deserialize<ProbePayload>(bytes.AsSpan());
Require(restored.Name == message.Name && restored.Amount == message.Amount);
var buffer = new ArrayBufferWriter<byte>(256);
serializer.Serialize(message, buffer);
Require(buffer.WrittenSpan.SequenceEqual(bytes));
foreach (bool array in new[] { false, true })
{
    byte[] trailing = Encoding.UTF8.GetBytes("{\"Name\":\"native\",\"Amount\":42.5} {}");
    try
    {
        _ = array ? serializer.Deserialize<ProbePayload>(trailing) : serializer.Deserialize<ProbePayload>(trailing.AsSpan());
        throw new InvalidOperationException("Trailing value accepted");
    }
    catch (SerializationException ex) when (ex.InnerException is JsonException) { checks++; }
    var options = new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip, MaxDepth = 2 };
    var configured = SystemTextJsonSerializer.Create(new ProbeContext(options));
    byte[] relaxed = Encoding.UTF8.GetBytes("/*before*/{\"Name\":\"configured\",\"Amount\":7,}/*after*/");
    var result = array ? configured.Deserialize<ProbePayload>(relaxed) : configured.Deserialize<ProbePayload>(relaxed.AsSpan());
    Require(result.Name == "configured" && result.Amount == 7);
    byte[] nullJson = "null"u8.ToArray();
    try
    {
        _ = array ? serializer.Deserialize<ProbePayload>(nullJson) : serializer.Deserialize<ProbePayload>(nullJson.AsSpan());
        throw new JsonException("Null result accepted");
    }
    catch (SerializationException ex) when (ex.Message.Contains("returned null", StringComparison.Ordinal)) { checks++; }
}
var composite = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(ProbeContext.Default.Options)
{ TypeInfoResolver = JsonTypeInfoResolver.Combine(OtherProbeContext.Default, ProbeContext.Default) });
byte[] compositeBytes = composite.SerializeToBytes(message);
Require(composite.Deserialize<ProbePayload>(compositeBytes).Name == "native");
Require(composite.Deserialize<ProbePayload>(compositeBytes.AsSpan()).Amount == message.Amount);
var compositeBuffer = new ArrayBufferWriter<byte>(256);composite.Serialize(message, compositeBuffer);
Require(compositeBuffer.WrittenSpan.SequenceEqual(compositeBytes));
foreach (bool array in new[] { false, true })
{
    var configuredContext = new ProbeContext(new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip });
    var configuredComposite = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions(configuredContext.Options)
    { TypeInfoResolver = JsonTypeInfoResolver.Combine(OtherProbeContext.Default, configuredContext, ProbeContext.Default) });
    byte[] relaxed = "/*before*/{\"Name\":\"composite\",\"Amount\":7,}/*after*/"u8.ToArray();
    for (int i = 0; i < 2; i++)
    {
        var result = array ? configuredComposite.Deserialize<ProbePayload>(relaxed) : configuredComposite.Deserialize<ProbePayload>(relaxed.AsSpan());
        Require(result.Name == "composite" && result.Amount == 7);
    }
    byte[] suffix = "{\"Name\":\"composite\"} {}"u8.ToArray();
    try { _ = array ? composite.Deserialize<ProbePayload>(suffix) : composite.Deserialize<ProbePayload>(suffix.AsSpan());throw new InvalidOperationException("Composite accepted suffix"); }
    catch (SerializationException ex) when (ex.InnerException is JsonException) { checks++; }
}
foreach (bool array in new[] { false, true })
{
    var shallow = SystemTextJsonSerializer.Create(new ProbeContext(new JsonSerializerOptions { MaxDepth = 1 }));
    byte[] nested = "{\"Child\":{\"Child\":null}}"u8.ToArray();
    try { _ = array ? shallow.Deserialize<ProbeNode>(nested) : shallow.Deserialize<ProbeNode>(nested.AsSpan());throw new InvalidOperationException("Depth ignored"); }
    catch (SerializationException ex) when (ex.InnerException is JsonException) { checks++; }
    var deep = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions
    { MaxDepth = 128, TypeInfoResolver = ProbeContext.Default });
    byte[] deepJson = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("{\"Child\":", 70)) + "null" + new string('}', 70));
    ProbeNode? node = array ? deep.Deserialize<ProbeNode>(deepJson) : deep.Deserialize<ProbeNode>(deepJson.AsSpan());
    int depth = 0;while (node is not null) { depth++;node = node.Child; }
    Require(depth == 70);
    var converter = new ProbeConverter();var converterOptions = new JsonSerializerOptions();converterOptions.Converters.Add(converter);
    var converterContext = new ProbeContext(converterOptions);
    var converted = SystemTextJsonSerializer.Create(converterContext);
    byte[] convertedJson = "  {\"Name\":\"from-converter\",\"Amount\":19}"u8.ToArray();
    var result = array ? converted.Deserialize<ProbePayload>(convertedJson) : converted.Deserialize<ProbePayload>(convertedJson.AsSpan());
    Require(result.Name == "from-converter" && result.Amount == 19);
    Require(ReferenceEquals(converter.Options, converterContext.GetTypeInfo(typeof(ProbePayload))!.Options) && converter.TokenStart == 2);
    var sentinel = new InvalidOperationException("native converter sentinel");converter.Failure = sentinel;
    try { _ = array ? converted.Deserialize<ProbePayload>(convertedJson) : converted.Deserialize<ProbePayload>(convertedJson.AsSpan());throw new JsonException("Converter exception lost"); }
    catch (SerializationException ex) when (ReferenceEquals(ex.InnerException, sentinel)) { checks++; }
}
foreach (string newLine in new[] { "\n", "\r\n" })
{
    var formattingOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        IndentCharacter = '\t',
        IndentSize = 1,
        NewLine = newLine,
    };
    var formattingContext = new ProbeContext(formattingOptions);
    var metadata = (JsonTypeInfo<ProbePayload>)formattingContext.GetTypeInfo(typeof(ProbePayload))!;
    byte[] direct = JsonSerializer.SerializeToUtf8Bytes(message, metadata);
    string exactJson = "{" + newLine + "\t\"Name\": \"native\"," + newLine
        + "\t\"Amount\": 42.5" + newLine + "}";
    Require(direct.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(exactJson)));
    var formattingCodec = SystemTextJsonSerializer.Create(formattingContext);
    Require(formattingCodec.SerializeObject(message, typeof(ProbePayload)).AsSpan().SequenceEqual(direct));
    var formattingBuffer = new ArrayBufferWriter<byte>(256);
    formattingCodec.Serialize(message, formattingBuffer);
    Require(formattingBuffer.WrittenSpan.SequenceEqual(direct));
}
var ownedPolicy = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    TypeInfoResolver = JsonTypeInfoResolver.Combine(EnvelopeProbeContext.Default, ProbeContext.Default),
};
var ownedCodec = SystemTextJsonSerializer.CreateWithMetadata(ownedPolicy);
var ownedNested = new ProbeEnvelope(message);
var nestedBytes = ownedCodec.SerializeToBytes(ownedNested);
Require(((JsonElement)ownedCodec.Deserialize<ProbeEnvelope>(nestedBytes).Data).GetProperty("name").GetString() == "native");
var services = new ServiceCollection();
services.AddSingleton<ILogger<PayloadSerializer>>(NullLogger<PayloadSerializer>.Instance);
services.AddPluggableSerializer(SerializerIds.SystemTextJson, ownedCodec, setAsCurrent: true);
using (var provider = services.BuildServiceProvider())
{
    var selected = provider.GetRequiredService<ISerializerRegistry>().GetCurrent();
    var payloads = provider.GetRequiredService<IPayloadSerializer>();
    var framed = payloads.Serialize(ownedNested);
    Require(selected.Id == SerializerIds.SystemTextJson && ReferenceEquals(selected.Serializer, ownedCodec)
        && framed[0] == SerializerIds.SystemTextJson && framed.AsSpan(1).SequenceEqual(nestedBytes));
    Require(((JsonElement)payloads.Deserialize<ProbeEnvelope>(framed).Data).GetProperty("name").GetString() == "native");
}
var missingChild = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions
{ TypeInfoResolver = EnvelopeProbeContext.Default });
try
{
    _ = missingChild.SerializeToBytes(ownedNested);
    throw new InvalidOperationException("Undeclared runtime child was accepted.");
}
catch (SerializationException ex) when (ex.InnerException is NotSupportedException) { checks++; }
Console.WriteLine("Owned JSON registration and nested payload contracts passed: 4 checks");
Console.WriteLine("SystemTextJsonSerializer effective writer formatting passed: 6 checks");
Console.WriteLine($"Combined native JSON contracts passed: {checks}; dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}");
}
}

public sealed class ProbePayload
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class ProbeNode
{
    public ProbeNode? Child { get; set; }
}

internal sealed class ProbeConverter : JsonConverter<ProbePayload>
{
    internal JsonSerializerOptions? Options { get; private set; }
    internal long TokenStart { get; private set; }
    internal Exception? Failure { get; set; }

    public override ProbePayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Options = options;TokenStart = reader.TokenStartIndex;
        if (Failure is not null) { throw Failure; }
        using var doc = JsonDocument.ParseValue(ref reader);
        return new ProbePayload { Name = doc.RootElement.GetProperty("Name").GetString()!, Amount = doc.RootElement.GetProperty("Amount").GetDecimal() };
    }

    public override void Write(Utf8JsonWriter writer, ProbePayload value, JsonSerializerOptions options) => throw new NotSupportedException();
}

[JsonSerializable(typeof(ProbePayload))]
[JsonSerializable(typeof(ProbeNode))]
internal sealed partial class ProbeContext : JsonSerializerContext;

[JsonSerializable(typeof(string))]
internal sealed partial class OtherProbeContext : JsonSerializerContext;

public sealed record ProbeEnvelope(object Data);

[JsonSerializable(typeof(ProbeEnvelope))]
internal sealed partial class EnvelopeProbeContext : JsonSerializerContext;
