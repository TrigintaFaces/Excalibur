// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using MemoryPack;
using Excalibur.Dispatch.Serialization;

// Diagnostic reflection and runtime loading do not establish NativeAOT compatibility.
var bundle = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, n) => File.Exists(Path.Combine(bundle, n.Name + ".dll"))
    ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, n.Name + ".dll")) : null;
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.Serialization.SpanEventSerializerBenchmarks", true)!;
var eventType = assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns.TestDomainEvent", true)!;
var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
Require(properties.Length == 7, "Event member population changed.");
foreach (var name in new[] { "EventId", "AggregateId", "Version", "OccurredAt", "EventType", "Metadata", "Data" })
    Require(properties.Any(p => p.Name == name), "Event member missing: " + name);
var setup = type.GetMethod("GlobalSetup")!;
var cleanup = type.GetMethod("GlobalCleanup")!;
Require(setup.ReturnType == typeof(void) && cleanup.ReturnType == typeof(void), "Lifecycle hook signature changed.");
var methods = type.GetMethods().Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "BenchmarkAttribute")).ToArray();
var expectedMethods = new[] { "JsonSerializeSmall", "MemPackSerializeSmallByteArray", "JsonSerializeLarge", "MemPackSerializeLargeByteArray",
    "JsonDeserializeSmall", "MemPackDeserializeSmallSpan", "JsonDeserializeLarge", "MemPackDeserializeLargeSpan",
    "MemPackSerializeSmallBufferWriter", "MemPackSerializeLargeBufferWriter", "JsonEventReplay100", "MemPackEventReplay100",
    "JsonEventAppend100", "MemPackEventAppend100", "JsonRoundTripSmall", "MemPackRoundTripSmall" };
Require(methods.Select(m => m.Name).Order().SequenceEqual(expectedMethods.Order()), "Benchmark method identities changed.");
var formatter = (IMemoryPackFormatter<IDictionary<string, object>>)Activator.CreateInstance(
    assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns.BenchmarkMetadataFormatter", true)!, true)!;
var negatives = 0;
var memberChecks = 0;
foreach (IDictionary<string, object>? metadata in new IDictionary<string, object>?[] {
    null, new Dictionary<string, object>(),
    new Dictionary<string, object> { [""] = "", ["unicode"] = "こんにちは 🌍", ["nullable"] = null! } })
{
    var bytes = EncodeMetadata(metadata);
    var decoded = DecodeMetadata(bytes);
    SameMetadata((IDictionary?)metadata, (IDictionary?)decoded); memberChecks++;
}
Reject(() => EncodeMetadata(new Dictionary<string, object> { ["unsupported"] = 42 }), "unsupported value");
Reject(() => EncodeMetadata(Enumerable.Range(0, 1025).ToDictionary(i => i.ToString(), _ => (object)"x")), "entry limit");
RejectMalformed(BitConverter.GetBytes(-2), "negative count");
RejectMalformed(BitConverter.GetBytes(int.MaxValue), "huge count");
RejectMalformed(BitConverter.GetBytes(1), "missing entries");
RejectMalformed(RawMetadata(2, ("same", "a"), ("same", "b")), "duplicate key");
RejectMalformed(RawMetadata(1, (null, "x")), "null key");
RejectMalformed(RawMetadata(2, ("one", "x")), "truncated entries");
RejectMalformed(BitConverter.GetBytes(1).Concat(BitConverter.GetBytes(int.MaxValue)).Concat(new byte[8]).ToArray(), "malformed string");
RejectMalformed(BitConverter.GetBytes(1025).Concat(new byte[1025 * 8]).ToArray(), "oversized count");
var beforeSelfCheck = negatives;
var selfRefused = false;
try { RejectMalformed(BitConverter.GetBytes(0).Concat(new byte[8]).ToArray(), "normal return with unread tail"); }
catch (ControlAssertionException) { selfRefused = true; }
Require(selfRefused && negatives == beforeSelfCheck, "Negative verifier accepted normal return with unread bytes.");
var serialize = typeof(SerializerExtensions).GetMethods().Single(m => m.Name == "SerializeToBytes" && m.IsGenericMethodDefinition).MakeGenericMethod(eventType);
var deserialize = typeof(SerializerExtensions).GetMethods().Single(m => m.Name == "Deserialize" && m.IsGenericMethodDefinition && m.GetParameters()[1].ParameterType == typeof(byte[])).MakeGenericMethod(eventType);
var cycles = 0;
long baseline = GC.GetTotalMemory(true), maxLive = baseline;
for (var repeat = 0; repeat < 8; repeat++)
{
    var fixture = Activator.CreateInstance(type)!;
    cleanup.Invoke(fixture, null); cleanup.Invoke(fixture, null);
    setup.Invoke(fixture, null);
    try
    {
        var small = Field(fixture, "_smallEvent")!;
        var large = Field(fixture, "_largeEvent")!;
        Require(((string)Get(large, "Data")!).Length == 8192, "Large data payload changed.");
        var mp = Field(fixture, "_memoryPackSerializer")!;
        foreach (var original in new[] { small, large })
        {
            var bytes = (byte[])serialize.Invoke(null, new[] { mp, original })!;
            SameEvent(original, deserialize.Invoke(null, new object[] { mp, bytes })!);
        }
        foreach (var metadata in new IDictionary<string, object>?[] { null, new Dictionary<string, object>(), new Dictionary<string, object> { ["null"] = null!, ["unicode"] = "λ🌍" } })
        {
            var clone = Activator.CreateInstance(eventType)!;
            foreach (var property in properties) property.SetValue(clone, property.Name == "Metadata" ? metadata : property.GetValue(small));
            var bytes = (byte[])serialize.Invoke(null, new[] { mp, clone })!;
            SameEvent(clone, deserialize.Invoke(null, new object[] { mp, bytes })!);
        }
        foreach (var method in methods)
        {
            var output = method.Invoke(fixture, null)!;
            var original = method.Name.Contains("Large") ? large : small;
            var expectedBytes = (byte[])Field(fixture, method.Name.StartsWith("Json")
                ? (method.Name.Contains("Large") ? "_largeEventJsonBytes" : "_smallEventJsonBytes")
                : (method.Name.Contains("Large") ? "_largeEventMemPackBytes" : "_smallEventMemPackBytes"))!;
            if (method.Name.Contains("Replay")) Require(output is int n && n == 100, "Replay did not return 100.");
            else if (method.Name.Contains("Append")) Require(output is int n && n == expectedBytes.Length * 100, "Append byte total changed.");
            else if (output is byte[] bytes) Require(bytes.SequenceEqual(expectedBytes), "Serialized bytes differ.");
            else if (output is int n) Require(n == expectedBytes.Length, "Written count differs.");
            else SameEvent(original, output, allowJsonElements: method.Name.StartsWith("Json"));
            cycles++;
        }
    }
    finally
    {
        cleanup.Invoke(fixture, null); cleanup.Invoke(fixture, null);
        Require(Field(fixture, "_pooledBuffer") is null, "Pooled buffer ownership retained after cleanup.");
    }
    maxLive = Math.Max(maxLive, GC.GetTotalMemory(true));
    Require(maxLive - baseline <= 64L * 1024 * 1024, "Control live-memory growth exceeded 64MiB.");
}
using var formatterState = MemoryPackReaderOptionalStatePool.Rent(null);
var formatterReader = new MemoryPackReader(ReadOnlySpan<byte>.Empty, formatterState);
var generatedFormatter = formatterReader.GetFormatter(eventType).GetType();
formatterReader.Dispose();
foreach (var dependency in new[] { typeof(MemoryPackReaderOptionalState).Assembly, typeof(ISerializer).Assembly })
    Require(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dependency.Location)).SequenceEqual(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(bundle, dependency.GetName().Name + ".dll")))),
        "Diagnostic reference differs from measured bundle.");
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, uniqueCases = methods.Length, repeats = 8, cycles,
    negativeControls = negatives, negativeVerifierSelfCheck = selfRefused, memberChecks, baselineLiveBytes = baseline, maxLiveBytes = maxLive,
    generatedFormatter = generatedFormatter.FullName, formatterAssembly = generatedFormatter.Assembly.FullName,
    methodNames = methods.Select(m => m.Name).Order().ToArray(), aotVerification = false,
    scope = "Full-field serializer fixture checks; fixture-specific string/null metadata, not general object metadata or measured latency" }));

object? Field(object obj, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj);
object? Get(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj);
void SameEvent(object expected, object actual, bool allowJsonElements = false)
{
    foreach (var property in properties.Where(p => p.Name != "Metadata"))
        Require(Equals(property.GetValue(expected), property.GetValue(actual)), "Round-trip field differs: " + property.Name);
    SameMetadata((IDictionary?)Get(expected, "Metadata"), (IDictionary?)Get(actual, "Metadata"), allowJsonElements);
}
void SameMetadata(IDictionary? expected, IDictionary? actual, bool allowJsonElements = false)
{
    if (expected is null) { Require(actual is null, "Null metadata became a dictionary."); return; }
    Require(actual is not null && actual.Count == expected.Count, "Metadata count differs.");
    foreach (DictionaryEntry pair in expected)
    {
        Require(actual!.Contains(pair.Key), "Metadata key missing.");
        var value = actual[pair.Key];
        if (allowJsonElements && value is JsonElement json)
        {
            Require(json.ValueKind is JsonValueKind.Null or JsonValueKind.String, "JSON metadata kind differs.");
            value = json.ValueKind == JsonValueKind.Null ? null : json.GetString();
        }
        Require(value is null or string, "Metadata CLR value type differs.");
        Require(Equals(pair.Value, value), "Metadata value differs.");
    }
}
byte[] EncodeMetadata(IDictionary<string, object>? value)
{
    var output = new ArrayBufferWriter<byte>();
    using var state = MemoryPackWriterOptionalStatePool.Rent(null);
    var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref output, state);
    formatter.Serialize(ref writer, ref value); writer.Flush();
    return output.WrittenSpan.ToArray();
}
IDictionary<string, object>? DecodeMetadata(byte[] bytes, bool requireFullyConsumed = true)
{
    using var state = MemoryPackReaderOptionalStatePool.Rent(null);
    var reader = new MemoryPackReader(bytes, state);
    IDictionary<string, object>? value = null;
    try { formatter.Deserialize(ref reader, ref value); if (requireFullyConsumed) Require(reader.Remaining == 0, "Unread metadata bytes."); return value; }
    finally { reader.Dispose(); }
}
byte[] RawMetadata(int count, params (string? Key, string? Value)[] pairs)
{
    var output = new ArrayBufferWriter<byte>();
    using var state = MemoryPackWriterOptionalStatePool.Rent(null);
    var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref output, state);
    writer.WriteCollectionHeader(count);
    foreach (var pair in pairs) { writer.WriteString(pair.Key); writer.WriteString(pair.Value); }
    writer.Flush(); return output.WrittenSpan.ToArray();
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (Exception e) when (e is InvalidOperationException or MemoryPackSerializationException)
    { negatives++; return; }
    throw new ControlAssertionException("Negative control accepted: " + name);
}
void RejectMalformed(byte[] bytes, string name) => Reject(() => DecodeMetadata(bytes, requireFullyConsumed: false), name);
static void Require(bool value, string message) { if (!value) throw new ControlAssertionException(message); }
sealed class ControlAssertionException(string message) : Exception(message);
