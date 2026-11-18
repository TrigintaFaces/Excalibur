// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections;
using System.Buffers;
using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

// R3 GWT: SDK and candidate always get separate events/payloads/options/resolvers/converters/streams.
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
[Trait("Pattern", "Conformance")]
public sealed class CloudEventMetadataFormatterConformanceShould
{
    private const string Prefix = "{\"specversion\":\"1.0\",\"id\":\"literal\",\"source\":\"/source\",\"type\":\"fixture\"";
    private const string Literal = Prefix + ",\"datacontenttype\":\"application/json\",\"data\":{\"name\":\"p\",\"count\":22}}";
    private const string Bare = Prefix + "}";
    public static TheoryData<string, string, string> EncodingCases => new()
    {
        { "P01", "poco", "structured" },
        { "P01", "poco", "binary" },
        { "P01", "poco", "batch" },
        { "P02", "root-converter", "structured" },
        { "P02", "root-converter", "binary" },
        { "P03", "ancestor", "structured" },
        { "P03", "ancestor", "binary" },
        { "P03", "ambiguous", "structured" },
        { "P03", "ambiguous", "binary" },
        { "P04", "payload-only", "structured" },
        { "P04", "payload-only", "binary" },
        { "P04", "payload-only", "batch" },
        { "P05", "missing", "structured" },
        { "P05", "missing", "binary" },
        { "P05", "missing", "batch" },
        { "P06", "converter-throw", "structured" },
        { "P06", "converter-throw", "binary" },
        { "P06", "converter-throw", "batch" },
        { "P06", "converter-scalar", "structured" },
        { "P06", "converter-scalar", "binary" },
        { "P06", "converter-scalar", "batch" },
        { "P06", "converter-array", "structured" },
        { "P06", "converter-array", "binary" },
        { "P06", "converter-array", "batch" },
        { "P06", "converter-object", "structured" },
        { "P06", "converter-object", "binary" },
        { "P06", "converter-object", "batch" },
        { "P07", "late-converter", "structured" },
        { "P07", "late-converter", "binary" },
        { "P07", "relaxed", "structured" },
        { "P07", "relaxed", "binary" },
        { "P08", "decoder-options", "structured" },
        { "P08", "decoder-options", "binary" },
        { "E01", "poco", "structured" },
        { "E01", "full-envelope", "structured" },
        { "E02", "typed", "structured" },
        { "E03", "unicode", "structured" },
        { "E03", "unicode", "binary" },
        { "E03", "relaxed", "structured" },
        { "E03", "relaxed", "binary" },
        { "E04", "null", "structured" },
        { "E04", "null-inferred", "structured" },
        { "E05", "bytes", "structured" },
        { "E05", "empty-bytes", "structured" },
        { "E06", "inferred", "structured" },
        { "E07", "suffix-json", "structured" },
        { "E07", "suffix-json", "binary" },
        { "E07", "slash-json", "structured" },
        { "E07", "slash-json", "binary" },
        { "E07", "upper-json", "structured" },
        { "E07", "upper-json", "binary" },
        { "E07", "json-quoted", "structured" },
        { "E07", "json-quoted", "binary" },
        { "E08", "unsupported", "structured" },
        { "E08", "bad-media", "structured" },
        { "E08", "text", "structured" },
        { "E08", "text-utf16", "structured" },
        { "E08", "unknown-charset", "structured" },
        { "B01", "null", "binary" },
        { "B01", "bytes", "binary" },
        { "B01", "empty-bytes", "binary" },
        { "B02", "poco", "binary" },
        { "B02", "json-utf16", "binary" },
        { "B02", "json-utf16be", "binary" },
        { "B02", "json-quoted", "binary" },
        { "B02", "empty-charset", "binary" },
        { "B02", "unknown-charset", "binary" },
        { "B02", "text", "binary" },
        { "B02", "text-utf16", "binary" },
        { "B03", "rom", "structured" },
        { "B03", "rom", "binary" },
        { "B03", "stream-payload", "structured" },
        { "B03", "stream-payload", "binary" },
        { "B03", "scalar", "structured" },
        { "B03", "scalar", "binary" },
        { "A01", "empty-batch", "batch" },
        { "A01", "poco", "batch" },
        { "A01", "two-batch", "batch" },
        { "A01", "mixed-batch", "batch" },
        { "A02", "poco", "batch" },
        { "A02", "iterator-throw", "batch" },
        { "A02", "current-throw", "batch" },
        { "A02", "null-member", "batch" },
        { "A02", "invalid-member", "batch" },
        { "A03", "null-event", "structured" },
        { "A03", "null-event", "binary" },
        { "A03", "invalid-event", "structured" },
        { "A03", "invalid-event", "binary" },
        { "A03", "null-batch", "batch" },
        { "A03", "invalid-member", "batch" },
    };
    public static TheoryData<string, string, int> DecodingCases => new()
    {
        { "P08", "decoder-options", 0 },
        { "P08", "decoder-options", 1 },
        { "P08", "decoder-options", 2 },
        { "D01", "literal", 0 },
        { "D01", "literal", 1 },
        { "D01", "literal", 2 },
        { "D01", "null-data", 0 },
        { "D01", "null-data", 1 },
        { "D01", "null-data", 2 },
        { "D01", "array-data", 0 },
        { "D01", "array-data", 1 },
        { "D01", "array-data", 2 },
        { "D01", "bool-data", 0 },
        { "D01", "bool-data", 1 },
        { "D01", "bool-data", 2 },
        { "D01", "string-data", 0 },
        { "D01", "string-data", 1 },
        { "D01", "string-data", 2 },
        { "D01", "number-data", 0 },
        { "D01", "number-data", 1 },
        { "D01", "number-data", 2 },
        { "D02", "empty-batch-decode", 0 },
        { "D02", "empty-batch-decode", 1 },
        { "D02", "empty-batch-decode", 2 },
        { "D02", "batch-literal", 0 },
        { "D02", "batch-literal", 1 },
        { "D02", "batch-literal", 2 },
        { "D03", "root-array", 0 },
        { "D03", "root-array", 1 },
        { "D03", "root-array", 2 },
        { "D03", "root-number", 0 },
        { "D03", "root-number", 1 },
        { "D03", "root-number", 2 },
        { "D03", "root-null", 0 },
        { "D03", "root-null", 1 },
        { "D03", "root-null", 2 },
        { "D03", "batch-nonarray", 0 },
        { "D03", "batch-nonarray", 1 },
        { "D03", "batch-nonarray", 2 },
        { "D03", "batch-invalid-member", 0 },
        { "D03", "batch-invalid-member", 1 },
        { "D03", "batch-invalid-member", 2 },
        { "D04", "missing-spec", 0 },
        { "D04", "missing-spec", 1 },
        { "D04", "missing-spec", 2 },
        { "D04", "null-spec", 0 },
        { "D04", "null-spec", 1 },
        { "D04", "null-spec", 2 },
        { "D04", "number-spec", 0 },
        { "D04", "number-spec", 1 },
        { "D04", "number-spec", 2 },
        { "D04", "unsupported-spec", 0 },
        { "D04", "unsupported-spec", 1 },
        { "D04", "unsupported-spec", 2 },
        { "D04", "spec03", 0 },
        { "D04", "spec03", 1 },
        { "D04", "spec03", 2 },
        { "D05", "wrong-core-token", 0 },
        { "D05", "wrong-core-token", 1 },
        { "D05", "wrong-core-token", 2 },
        { "D05", "source-object", 0 },
        { "D05", "source-object", 1 },
        { "D05", "source-object", 2 },
        { "D05", "type-bool", 0 },
        { "D05", "type-bool", 1 },
        { "D05", "type-bool", 2 },
        { "D05", "time-number", 0 },
        { "D05", "time-number", 1 },
        { "D05", "time-number", 2 },
        { "D05", "null-required", 0 },
        { "D05", "null-required", 1 },
        { "D05", "null-required", 2 },
        { "D05", "null-optional", 0 },
        { "D05", "null-optional", 1 },
        { "D05", "null-optional", 2 },
        { "D05", "missing-required", 0 },
        { "D05", "missing-required", 1 },
        { "D05", "missing-required", 2 },
        { "D06", "typed-extensions", 0 },
        { "D06", "typed-extensions", 1 },
        { "D06", "typed-extensions", 2 },
        { "D06", "typed-native", 0 },
        { "D06", "typed-native", 1 },
        { "D06", "typed-native", 2 },
        { "D06", "bad-typed-extension", 0 },
        { "D06", "bad-typed-extension", 1 },
        { "D06", "bad-typed-extension", 2 },
        { "D07", "unknown-extensions", 0 },
        { "D07", "unknown-extensions", 1 },
        { "D07", "unknown-extensions", 2 },
        { "D07", "fraction", 0 },
        { "D07", "fraction", 1 },
        { "D07", "fraction", 2 },
        { "D07", "overflow", 0 },
        { "D07", "overflow", 1 },
        { "D07", "overflow", 2 },
        { "D07", "extension-array", 0 },
        { "D07", "extension-array", 1 },
        { "D07", "extension-array", 2 },
        { "D07", "extension-object", 0 },
        { "D07", "extension-object", 1 },
        { "D07", "extension-object", 2 },
        { "D08", "duplicates", 0 },
        { "D08", "duplicates", 1 },
        { "D08", "duplicates", 2 },
        { "D08", "duplicate-spec", 0 },
        { "D08", "duplicate-spec", 1 },
        { "D08", "duplicate-spec", 2 },
        { "D08", "duplicate-base64", 0 },
        { "D08", "duplicate-base64", 1 },
        { "D08", "duplicate-base64", 2 },
        { "D09", "both-data", 0 },
        { "D09", "both-data", 1 },
        { "D09", "both-data", 2 },
        { "D09", "data-null-base64", 0 },
        { "D09", "data-null-base64", 1 },
        { "D09", "data-null-base64", 2 },
        { "D09", "data-base64-null", 0 },
        { "D09", "data-base64-null", 1 },
        { "D09", "data-base64-null", 2 },
        { "D09", "both-data-null", 0 },
        { "D09", "both-data-null", 1 },
        { "D09", "both-data-null", 2 },
        { "D09", "literal", 0 },
        { "D09", "literal", 1 },
        { "D09", "literal", 2 },
        { "D09", "batch-both-data", 0 },
        { "D09", "batch-both-data", 1 },
        { "D09", "batch-both-data", 2 },
        { "D10", "base64-empty", 0 },
        { "D10", "base64-empty", 1 },
        { "D10", "base64-empty", 2 },
        { "D10", "base64-malformed", 0 },
        { "D10", "base64-malformed", 1 },
        { "D10", "base64-malformed", 2 },
        { "D10", "base64-number", 0 },
        { "D10", "base64-number", 1 },
        { "D10", "base64-number", 2 },
        { "D10", "batch-bad-base64", 0 },
        { "D10", "batch-bad-base64", 1 },
        { "D10", "batch-bad-base64", 2 },
        { "D11", "nonjson-string", 0 },
        { "D11", "nonjson-string", 1 },
        { "D11", "nonjson-string", 2 },
        { "D11", "nonjson-number", 0 },
        { "D11", "nonjson-number", 1 },
        { "D11", "nonjson-number", 2 },
        { "D11", "nonjson-object", 0 },
        { "D11", "nonjson-object", 1 },
        { "D11", "nonjson-object", 2 },
        { "D12", "null-contenttype", 0 },
        { "D12", "null-contenttype", 1 },
        { "D12", "null-contenttype", 2 },
        { "D12", "unusual-contenttype", 0 },
        { "D12", "unusual-contenttype", 1 },
        { "D12", "unusual-contenttype", 2 },
        { "D13", "trailing-comma", 0 },
        { "D13", "trailing-comma", 1 },
        { "D13", "trailing-comma", 2 },
        { "D13", "comments", 0 },
        { "D13", "comments", 1 },
        { "D13", "comments", 2 },
        { "D13", "depth63", 0 },
        { "D13", "depth63", 1 },
        { "D13", "depth63", 2 },
        { "D13", "depth64", 0 },
        { "D13", "depth64", 1 },
        { "D13", "depth64", 2 },
        { "D13", "deep", 0 },
        { "D13", "deep", 1 },
        { "D13", "deep", 2 },
        { "D13", "invalid-utf8", 0 },
        { "D13", "invalid-utf8", 1 },
        { "D13", "invalid-utf8", 2 },
        { "D13", "malformed", 0 },
        { "D13", "malformed", 1 },
        { "D13", "malformed", 2 },
        { "L01", "lifetime", 0 },
        { "L01", "lifetime", 1 },
        { "L01", "lifetime", 2 },
        { "L02", "batch-lifetime", 0 },
        { "L02", "batch-lifetime", 1 },
        { "L02", "batch-lifetime", 2 },
        { "S01", "nonseekable", 1 },
        { "S01", "nonseekable", 2 },
        { "S01", "short-reads", 1 },
        { "S01", "short-reads", 2 },
        { "S01", "position", 1 },
        { "S01", "position", 2 },
        { "S01", "disposed", 1 },
        { "S01", "disposed", 2 },
        { "S02", "async-only", 2 },
        { "S03", "utf16", 0 },
        { "S03", "utf16", 1 },
        { "S03", "utf16", 2 },
        { "S03", "utf16be", 0 },
        { "S03", "utf16be", 1 },
        { "S03", "utf16be", 2 },
        { "S03", "utf8-bom", 0 },
        { "S03", "utf8-bom", 1 },
        { "S03", "utf8-bom", 2 },
        { "S03", "bom-conflict", 0 },
        { "S03", "bom-conflict", 1 },
        { "S03", "bom-conflict", 2 },
        { "S03", "utf16-malformed", 0 },
        { "S03", "utf16-malformed", 1 },
        { "S03", "utf16-malformed", 2 },
        { "S03", "batch-utf16", 0 },
        { "S03", "batch-utf16", 1 },
        { "S03", "batch-utf16", 2 },
        { "S04", "null-stream", 1 },
        { "S04", "null-stream", 2 },
        { "S04", "null-batch-stream", 1 },
        { "S04", "null-batch-stream", 2 },
        { "S04", "io-error", 1 },
        { "S04", "io-error", 2 },
        { "S04", "unsupported-charset", 1 },
        { "S04", "unsupported-charset", 2 },
        { "S04", "malformed", 1 },
        { "S04", "malformed", 2 },
        { "S05", "literal", 0 },
        { "S05", "literal", 1 },
        { "S05", "literal", 2 },
        { "S05", "batch-literal", 0 },
        { "S05", "batch-literal", 1 },
        { "S05", "batch-literal", 2 },
        { "S05", "both-data", 0 },
        { "S05", "both-data", 1 },
        { "S05", "both-data", 2 },
        { "S05", "base64-number", 0 },
        { "S05", "base64-number", 1 },
        { "S05", "base64-number", 2 },
        { "D01", "nonarray", 0 },
        { "L01", "nonarray-lifetime", 0 },
        { "S02", "batch-async-only", 2 },
        { "S01", "unreadable", 1 },
        { "S01", "unreadable", 2 },
        { "S03", "utf16-bom", 0 },
        { "S03", "utf16-bom", 1 },
        { "S03", "utf16-bom", 2 },
    };
    public static TheoryData<string, string, string> BinaryDecodingCases => new()
    {
        { "BD01", "application/json", "json" },
        { "BD01", "application/json", "empty" },
        { "BD01", "application/json", "scalar" },
        { "BD01", "application/json", "array" },
        { "BD01", "default", "json" },
        { "BD01", "default", "empty" },
        { "BD01", "default", "scalar" },
        { "BD01", "default", "array" },
        { "BD02", "application/problem+json", "json" },
        { "BD02", "application/problem+json", "empty" },
        { "BD02", "text/json", "json" },
        { "BD02", "text/json", "empty" },
        { "BD02", "text/plain", "json" },
        { "BD02", "text/plain", "empty" },
        { "BD02", "application/octet-stream", "json" },
        { "BD02", "application/octet-stream", "empty" },
        { "BD03", "application/json", "json" },
        { "BD03", "text/plain", "json" },
        { "BD03", "application/octet-stream", "json" },
        { "BD04", "application/json", "empty" },
        { "BD04", "application/json", "incomplete" },
        { "BD04", "application/json", "invalid" },
        { "BD04", "application/json; charset=utf-16", "empty" },
        { "BD04", "application/json; charset=utf-16", "incomplete" },
        { "BD04", "application/json; charset=utf-16", "invalid" },
        { "BD04", "application/json; charset=not-a-real-encoding", "empty" },
        { "BD04", "application/json; charset=not-a-real-encoding", "incomplete" },
        { "BD04", "application/json; charset=not-a-real-encoding", "invalid" },
        { "BD04", "text/plain; charset=not-a-real-encoding", "empty" },
        { "BD04", "text/plain; charset=not-a-real-encoding", "incomplete" },
        { "BD04", "text/plain; charset=not-a-real-encoding", "invalid" },
        { "BD04", "application/octet-stream; charset=not-a-real-encoding", "empty" },
        { "BD04", "application/octet-stream; charset=not-a-real-encoding", "incomplete" },
        { "BD04", "application/octet-stream; charset=not-a-real-encoding", "invalid" },
    };

    [Theory]
    [MemberData(nameof(EncodingCases))]
    public void PreserveEncodingBytesErrorsAndBothIndependentStates(string group, string fixture, string operation)
    {
        if (fixture == "bad-media")
        {
            // This exact malformed value is rejected by CloudEvent.DataContentType during Make.
            // Capture each independent fixture construction: MODEL validation, not a formatter pass.
            var expectedConstruction = Error(() => { using var value = Make(fixture, metadata: false); });
            var actualConstruction = Error(() => { using var value = Make(fixture, metadata: true); });
            actualConstruction.ShouldBe(expectedConstruction, customMessage: $"{group}: model fixture construction");
            actualConstruction.ShouldNotBeNull();
            actualConstruction.ShouldStartWith(typeof(ArgumentException).FullName + "|value|");
            return;
        }

        using var oracle = Make(fixture, metadata: false);
        using var actual = Make(fixture, metadata: true);
        actual.Options.ShouldNotBeSameAs(oracle.Options);
        actual.Resolver.ShouldNotBeSameAs(oracle.Resolver);
        if (actual.Event is not null) actual.Event.ShouldNotBeSameAs(oracle.Event);
        if (fixture == "payload-only") { oracle.Resolver.GetTypeInfo(typeof(object), oracle.Options).ShouldBeNull(); actual.Resolver.GetTypeInfo(typeof(object), actual.Options).ShouldBeNull(); }
        var expectedEvent = oracle.Event is null ? "null" : EventState(oracle.Event);
        var actualEvent = actual.Event is null ? "null" : EventState(actual.Event);
        var expected = Encode(oracle, fixture, operation);
        var result = Encode(actual, fixture, operation);
        Match(expected, result);
        (oracle.Event is null ? "null" : EventState(oracle.Event)).ShouldBe(expectedEvent);
        (actual.Event is null ? "null" : EventState(actual.Event)).ShouldBe(actualEvent);
        if (fixture is "poco" or "payload-only" or "ancestor")
        {
            oracle.Resolver.SawCoveredMetadata.ShouldBeTrue(); actual.Resolver.SawCoveredMetadata.ShouldBeTrue();
            oracle.Options.GetTypeInfo(typeof(object)).Options.ShouldBeSameAs(oracle.Options);
            actual.Options.GetTypeInfo(typeof(object)).Options.ShouldBeSameAs(actual.Options);
        }
        if (fixture == "ancestor") result.Error.ShouldBeNull();
        if (fixture == "ambiguous") result.Error.ShouldContain(typeof(NotSupportedException).FullName!);
        if (fixture == "poco" && operation == "structured")
        {
            expected.Error.ShouldBeNull();
            Encoding.UTF8.GetString(expected.Bytes!).ShouldBe(Literal);
            Encoding.UTF8.GetString(result.Bytes!).ShouldBe(Literal);
            result.ContentType.ShouldBe("application/cloudevents+json; charset=utf-8");
        }
        if (fixture == "poco" && operation == "binary") result.Bytes.ShouldBe(Encoding.UTF8.GetBytes("{\"name\":\"p\",\"count\":22}"));
        if (fixture == "empty-batch") result.Bytes.ShouldBe(Encoding.UTF8.GetBytes("[]"));
        if (fixture is "root-converter" or "late-converter")
        {
            actual.Converter!.Writes.ShouldBe(1); oracle.Converter!.Writes.ShouldBe(1);
            using var json = JsonDocument.Parse(result.Bytes!);
            (operation == "structured" ? json.RootElement.GetProperty("data") : json.RootElement).GetString().ShouldBe("root-converter");
        }
        if (fixture == "missing")
        {
            result.Error.ShouldNotBeNull(); result.Error.ShouldContain(nameof(MetadataMissingPayload));
            result.Error.ShouldContain(typeof(NotSupportedException).FullName!);
        }
        if (group == "B01" && fixture == "bytes")
        {
            var bytes = new byte[] { 1, 2, 255 };
            var memory = actual.Formatter.EncodeBinaryModeEventData(Event(bytes)); bytes[0] = 9;
            memory.Span[0].ShouldBe((byte)9);
        }
        if (fixture == "null" && operation == "binary") result.Bytes!.Length.ShouldBe(0);
        if (operation == "binary" && (fixture is "json-utf16" or "json-utf16be" or "json-quoted"))
        {
            var encoding = fixture == "json-utf16" ? Encoding.Unicode : fixture == "json-utf16be" ? Encoding.BigEndianUnicode : Encoding.UTF8;
            result.Bytes.ShouldBe(encoding.GetBytes("{\"name\":\"p\",\"count\":22}"));
        }
        if (fixture == "text-utf16" && operation == "binary") result.Bytes.ShouldBe(Encoding.Unicode.GetBytes("snow-雪"));
        if (fixture == "null" && operation == "structured") Encoding.UTF8.GetString(result.Bytes!).ShouldBe(Prefix + ",\"datacontenttype\":\"application/json\"}");
        if (fixture == "null-inferred") Encoding.UTF8.GetString(result.Bytes!).ShouldBe(Bare);
        if (fixture == "bytes" && operation == "structured") Encoding.UTF8.GetString(result.Bytes!).ShouldBe(Prefix + ",\"datacontenttype\":\"application/json\",\"data_base64\":\"AQL/\"}");
        if (fixture == "empty-bytes" && operation == "structured") Encoding.UTF8.GetString(result.Bytes!).ShouldBe(Prefix + ",\"datacontenttype\":\"application/json\",\"data_base64\":\"\"}");
        if (fixture == "typed")
        {
            using var json = JsonDocument.Parse(result.Bytes!);
            json.RootElement.GetProperty("flag").ValueKind.ShouldBe(JsonValueKind.False);
            json.RootElement.GetProperty("number").GetInt32().ShouldBe(int.MinValue);
            json.RootElement.GetProperty("binary").GetString().ShouldBe("AQI=");
        }
    }

    [Theory]
    [MemberData(nameof(DecodingCases))]
    public async Task PreserveLiteralDecodingErrorsLifetimesAndStreamHistory(string group, string fixture, int mode)
    {
        using var oracle = Make(fixture == "decoder-options" ? fixture : "poco", metadata: false);
        using var actual = Make(fixture == "decoder-options" ? fixture : "poco", metadata: true);
        var expected = await Decode(oracle, fixture, mode);
        var result = await Decode(actual, fixture, mode);
        Match(expected, result, group);
        if (fixture == "null-batch-stream")
        {
            result.Error.ShouldNotBeNull();
            result.Error.ShouldStartWith(typeof(ArgumentNullException).FullName + "|data|");
        }
        if (fixture is "literal" or "lifetime" or "batch-lifetime" or "nonarray" or "nonarray-lifetime")
        {
            result.Error.ShouldBeNull(); result.Decoded.ShouldContain("name"); result.Decoded.ShouldContain("p");
        }
        if (fixture == "decoder-options") result.Error.ShouldNotBeNull();
        if (fixture == "unknown-extensions")
        {
            result.Decoded.ShouldContain("unknownflag:System.String:false"); result.Decoded.ShouldContain("unknownnumber:System.String:22");
        }
        if (fixture is "typed-extensions" or "typed-native")
        {
            result.Decoded.ShouldContain("flag:System.Boolean:false"); result.Decoded.ShouldContain("number:System.Int32:22");
        }
        if (mode != 0 && (fixture is "utf16" or "utf16-bom" or "utf16be" or "bom-conflict" or "utf16-malformed" or "batch-utf16")) result.StreamState.ShouldContain("after:disposed=True");
        if (mode != 0 && fixture == "literal") result.StreamState.ShouldContain("after:disposed=False");
        if ((fixture is "async-only" or "batch-async-only") && mode == 2)
        {
            result.Error.ShouldBeNull(); result.StreamState.ShouldContain("sync=False"); result.StreamState.ShouldContain("async=True");
        }
    }

    [Theory]
    [MemberData(nameof(BinaryDecodingCases))]
    public void PreserveBinaryDecodeAsymmetryDataOnlyMutationAndCopies(string group, string media, string body)
    {
        using var oracle = Make("poco", metadata: false); using var actual = Make("poco", metadata: true);
        var expected = DecodeBinary(oracle, media, body); var result = DecodeBinary(actual, media, body); Match(expected, result, group);
        if (media == "application/problem+json" && body == "json") result.Decoded.ShouldStartWith("bytes:");
        if (media == "application/json" && body == "empty") result.Decoded.ShouldBe("null");
        if (media == "application/json" && body == "json") result.Decoded.ShouldStartWith("json:");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R01KeepGuardOrderPublicRegistrationSingletonAndOptions(bool guardFirst)
    {
        var services = new ServiceCollection();
        if (guardFirst) services.AddCloudEventSerializerGuard();
        services.AddDispatch(builder => builder.AddCloudEvents(new MetadataConformanceContext()));
        if (!guardFirst) services.AddCloudEventSerializerGuard();
        using var provider = services.BuildServiceProvider(); var serializer = provider.GetRequiredService<CloudEventSerializer>();
        serializer.ShouldBeSameAs(provider.GetRequiredService<CloudEventSerializer>()); serializer.Options.IsReadOnly.ShouldBeTrue();
        Encoding.UTF8.GetString(serializer.Formatter.EncodeStructuredModeMessage(Event(new MetadataConformancePayload { Name = "p", Count = 22 }), out _).Span).ShouldBe(Literal);
    }

    [Fact]
    public void R02RetainLegacyConstructorAndExplicitCustomDiSelection()
    {
        var options = new JsonSerializerOptions(); options.TypeInfoResolver.ShouldBeNull();
        var services = new ServiceCollection(); services.AddDispatch(builder => builder.AddCloudEvents(new MetadataConformanceContext()));
        services.AddSingleton<CloudEventSerializer>(_ => new CloudEventSerializer(options));
        using var provider = services.BuildServiceProvider(); var selected = provider.GetRequiredService<CloudEventSerializer>();
        selected.Options.ShouldBeSameAs(options); selected.Formatter.GetType().ShouldBe(typeof(JsonEventFormatter));
        var oracle = new JsonEventFormatter(new JsonSerializerOptions(), default);
        selected.Formatter.EncodeStructuredModeMessage(Event(new MetadataMissingPayload { Value = "legacy" }), out _).ToArray()
            .ShouldBe(oracle.EncodeStructuredModeMessage(Event(new MetadataMissingPayload { Value = "legacy" }), out _).ToArray());
    }

    [Fact]
    public void R03RetainFactoryArgumentErrorsWithoutAddingFallback()
    {
        Should.Throw<ArgumentNullException>(() => CloudEventSerializer.CreateWithMetadata(null!)).ParamName.ShouldBe("options");
        var raw = new JsonSerializerOptions(); Should.Throw<ArgumentException>(() => CloudEventSerializer.CreateWithMetadata(raw)).ParamName.ShouldBe("options");
        raw.TypeInfoResolver.ShouldBeNull(); raw.IsReadOnly.ShouldBeFalse();
        var options = new JsonSerializerOptions { TypeInfoResolver = new EmptyMetadataResolver() };
        var serializer = CloudEventSerializer.CreateWithMetadata(options); serializer.Options.ShouldBeSameAs(options); options.IsReadOnly.ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => serializer.SerializePayload(new MetadataMissingPayload())).Message.ShouldContain(nameof(MetadataMissingPayload));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("bytes")]
    [InlineData("poco")]
    [InlineData("text")]
    [InlineData("explicit")]
    public void I01PreserveInferenceWithoutCallerMutation(string fixture)
    {
        using var oracle = Make(fixture, metadata: false); using var actual = Make(fixture, metadata: true);
        oracle.Event!.DataContentType = fixture == "explicit" ? "text/custom" : null; actual.Event!.DataContentType = fixture == "explicit" ? "text/custom" : null;
        var eb = State(oracle); var ab = State(actual);
        actual.Formatter.GetOrInferDataContentType(actual.Event).ShouldBe(oracle.Formatter.GetOrInferDataContentType(oracle.Event));
        State(oracle).ShouldBe(eb); State(actual).ShouldBe(ab);
        actual.Formatter.GetOrInferDataContentType(actual.Event).ShouldBe(fixture is "null" or "bytes" ? null : fixture == "explicit" ? "text/custom" : "application/json");
        Error(() => oracle.Formatter.GetOrInferDataContentType(null!)).ShouldBe(Error(() => actual.Formatter.GetOrInferDataContentType(null!)));
    }

    [Fact]
    public async Task H01ReuseFrozenFormatterAcrossIndependentConcurrentEvents()
    {
        using var oracle = Make("poco", metadata: false); using var actual = Make("poco", metadata: true);
        oracle.Options.MakeReadOnly(); actual.Options.MakeReadOnly(); var eb = State(oracle); var ab = State(actual);
        var oracleEvents = Enumerable.Range(0, 12).Select(index => Event(new MetadataConformancePayload { Name = "p", Count = 22 })).ToArray();
        var actualEvents = Enumerable.Range(0, 12).Select(index => Event(new MetadataConformancePayload { Name = "p", Count = 22 })).ToArray();
        var oracleBefore = oracleEvents.Select(EventState).ToArray(); var actualBefore = actualEvents.Select(EventState).ToArray();
        var expected = await Task.WhenAll(oracleEvents.Select(value => Task.Run(() => oracle.Formatter.EncodeStructuredModeMessage(value, out _).ToArray())));
        var results = await Task.WhenAll(actualEvents.Select(value => Task.Run(() => actual.Formatter.EncodeStructuredModeMessage(value, out _).ToArray())));
        oracleEvents.Select(EventState).ToArray().ShouldBe(oracleBefore); actualEvents.Select(EventState).ToArray().ShouldBe(actualBefore);
        for (var i = 0; i < results.Length; i++) { results[i].ShouldBe(expected[i]); Encoding.UTF8.GetString(results[i]).ShouldBe(Literal); }
        State(oracle).ShouldBe(eb); State(actual).ShouldBe(ab);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void P05RefuseMissingMetadataThroughBothPublicPayloadApis(bool utf8)
    {
        using var oracle = Make("missing", metadata: false); using var actual = Make("missing", metadata: true);
        var eb = State(oracle); var ab = State(actual);
        var expected = Error(() =>
        {
            var payload = oracle.Event!.Data!;
            if (utf8) JsonSerializer.SerializeToUtf8Bytes(payload, oracle.Options.GetTypeInfo(payload.GetType()));
            else JsonSerializer.Serialize(payload, oracle.Options.GetTypeInfo(payload.GetType()));
        });
        var serializer = CloudEventSerializer.CreateWithMetadata(actual.Options);
        var result = Error(() =>
        {
            if (utf8) serializer.SerializePayloadToUtf8Bytes(actual.Event!.Data!);
            else serializer.SerializePayload(actual.Event!.Data!);
        });
        eb.ShouldBe(ab); State(actual).ShouldBe(State(oracle)); result.ShouldBe(expected);
        result.ShouldNotBeNull(); result.ShouldContain(nameof(MetadataMissingPayload));
        serializer.Options.ShouldBeSameAs(actual.Options);
    }

    private static Observation Encode(Bundle bundle, string fixture, string operation)
    {
        var before = State(bundle); ContentType output = new("sentinel/example"); byte[]? bytes = null;
        var sequence = new OneShotEvents(bundle.Event, fixture);
        var error = Error(() =>
        {
            bytes = operation switch
            {
                "binary" => bundle.Formatter.EncodeBinaryModeEventData(bundle.Event!).ToArray(),
                "batch" => bundle.Formatter.EncodeBatchModeMessage(fixture == "null-batch" ? null! : sequence, out output).ToArray(),
                _ => bundle.Formatter.EncodeStructuredModeMessage(bundle.Event!, out output).ToArray(),
            };
        });
        return new(bytes, output.ToString(), error, before, State(bundle), "", sequence.State, "");
    }

    private static async Task<Observation> Decode(Bundle bundle, string fixture, int mode)
    {
        var input = Input(fixture); var before = State(bundle); var bytes = input.Bytes.ToArray();
        using var nonArray = new NonArrayMemory(bytes);
        ReadOnlyMemory<byte> memoryBody = fixture is "nonarray" or "nonarray-lifetime" ? nonArray.Memory : bytes;
        using var stream = new WitnessStream(bytes, fixture is "nonseekable" or "async-only" or "batch-async-only", fixture == "short-reads", fixture is "async-only" or "batch-async-only", fixture == "io-error", fixture == "unreadable");
        if (fixture == "position") stream.SkipPrefix(3);
        if (fixture == "disposed") stream.Dispose();
        var streamBefore = mode == 0 ? "rom" : stream.State;
        before += ";body=" + Convert.ToBase64String(bytes) + ";stream=" + streamBefore;
        IReadOnlyList<CloudEvent>? events = null; Exception? failure = null;
        try
        {
            var extensions = Extensions(fixture);
            if (input.Batch)
            {
                events = mode switch
                {
                    0 => bundle.Formatter.DecodeBatchModeMessage(memoryBody, input.ContentType, extensions),
                    1 => bundle.Formatter.DecodeBatchModeMessage(fixture is "null-stream" or "null-batch-stream" ? null! : stream, input.ContentType, extensions),
                    _ => await bundle.Formatter.DecodeBatchModeMessageAsync(fixture is "null-stream" or "null-batch-stream" ? null! : stream, input.ContentType, extensions),
                };
            }
            else
            {
                var value = mode switch
                {
                    0 => bundle.Formatter.DecodeStructuredModeMessage(memoryBody, input.ContentType, extensions),
                    1 => bundle.Formatter.DecodeStructuredModeMessage(fixture == "null-stream" ? null! : stream, input.ContentType, extensions),
                    _ => await bundle.Formatter.DecodeStructuredModeMessageAsync(fixture == "null-stream" ? null! : stream, input.ContentType, extensions),
                };
                events = [value];
            }
        }
        catch (Exception error) { failure = error; }
        bytes.ShouldBe(input.Bytes);
        // Decoder's internal document is already disposed. Retained JSON must survive input destruction.
        if (fixture is "lifetime" or "batch-lifetime" or "nonarray-lifetime") { Array.Fill(bytes, (byte)'!'); stream.Dispose(); ((IDisposable)nonArray).Dispose(); GC.Collect(); }
        if (mode != 0 && (fixture is "position" or "nonseekable" or "short-reads")) failure.ShouldBeNull();
        var decoded = events is null ? "" : "count=" + events.Count + "|" + string.Join("||", events.Select(EventState));
        return new(null, "", ErrorState(failure), before, State(bundle), decoded, "", mode == 0 ? "rom" : "before:" + streamBefore + ";after:" + stream.State);
    }

    private static Observation DecodeBinary(Bundle bundle, string media, string body)
    {
        var value = body == "incomplete" ? new CloudEvent() : Event("before");
        value.DataContentType = media == "default" ? null : media;
        var bytes = body switch
        {
            "empty" or "incomplete" => (byte[])Array.Empty<byte>().Clone(),
            "invalid" => Encoding.UTF8.GetBytes("{invalid"),
            "scalar" => Encoding.UTF8.GetBytes("22"),
            "array" => Encoding.UTF8.GetBytes("[true,22]"),
            _ => (media.Contains("utf-16", StringComparison.Ordinal) ? Encoding.Unicode : Encoding.UTF8).GetBytes("{\"name\":\"p\",\"count\":22}"),
        };
        var before = State(bundle) + ";event=" + EventState(value); var attributes = Attributes(value);
        var error = Error(() => bundle.Formatter.DecodeBinaryModeEventData(bytes, value));
        Array.Fill(bytes, (byte)'!'); Attributes(value).ShouldBe(attributes);
        return new(null, "", error, before, State(bundle) + ";event=" + EventState(value), DataState(value.Data), "", "");
    }

    private static Bundle Make(string fixture, bool metadata)
    {
        var resolver = new FreshMetadataResolver(fixture);
        var options = fixture == "payload-only" ? new JsonSerializerOptions { TypeInfoResolver = resolver } : new JsonSerializerOptions(CloudEventPayloadSerialization.CreateOptions(resolver));
        if (fixture is "relaxed" or "late-converter") { options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping; options.WriteIndented = true; }
        if (fixture == "decoder-options") { options.MaxDepth = 1; options.AllowTrailingCommas = true; options.ReadCommentHandling = JsonCommentHandling.Skip; }
        RootWitnessConverter? converter = null;
        if (fixture.StartsWith("converter-", StringComparison.Ordinal) || fixture == "root-converter") { converter = new RootWitnessConverter(fixture); options.Converters.Add(converter); }
        var formatter = metadata ? CloudEventSerializer.CreateWithMetadata(options).Formatter : new JsonEventFormatter(options, default);
        if (fixture == "late-converter") { converter = new RootWitnessConverter(fixture); options.Converters.Add(converter); }
        object? payload = fixture switch
        {
            "null" or "null-inferred" => null,
            "bytes" => new byte[] { 1, 2, 255 },
            "empty-bytes" => (byte[])Array.Empty<byte>().Clone(),
            "text" or "text-utf16" => "snow-雪",
            "unicode" => new MetadataConformancePayload { Name = "quote\"/\\<雪>🚀", Count = 22 },
            "relaxed" => new MetadataConformancePayload { Name = "<雪>\"/", Count = 22 },
            "missing" => new MetadataMissingPayload(),
            "ancestor" => new MetadataPrivateAncestor { Name = "ancestor" },
            "ambiguous" => new MetadataAmbiguousAncestor { Name = "ambiguous" },
            "rom" => new ReadOnlyMemory<byte>(new byte[] { 1, 2 }),
            "stream-payload" => new MemoryStream(new byte[] { 1, 2 }),
            "scalar" => 22,
            _ => new MetadataConformancePayload { Name = "p", Count = 22 },
        };
        var value = fixture == "null-event" ? null : Event(payload);
        if (value is not null)
        {
            value.DataContentType = fixture switch
            {
                "text" or "unsupported" => "text/plain",
                "text-utf16" => "text/plain; charset=utf-16",
                "json-utf16" => "application/json; charset=utf-16",
                "json-utf16be" => "application/json; charset=utf-16BE",
                "json-quoted" => "application/json; charset=\"utf-8\"",
                "unknown-charset" => "application/json; charset=not-a-real-encoding",
                "empty-charset" => "application/json; charset=\"\"",
                "suffix-json" => "application/problem+json",
                "slash-json" => "text/json",
                "upper-json" => "APPLICATION/JSON",
                "bad-media" => "not-a-content-type;=",
                "inferred" or "null-inferred" => null,
                _ => value.DataContentType,
            };
            if (fixture == "invalid-event") value.Id = null;
            if (fixture == "typed")
            {
                value[CloudEventAttribute.CreateExtension("flag", CloudEventAttributeType.Boolean)] = false;
                value[CloudEventAttribute.CreateExtension("number", CloudEventAttributeType.Integer)] = int.MinValue;
                value[CloudEventAttribute.CreateExtension("uppernumber", CloudEventAttributeType.Integer)] = int.MaxValue;
                value[CloudEventAttribute.CreateExtension("binary", CloudEventAttributeType.Binary)] = new byte[] { 1, 2 };
                value[CloudEventAttribute.CreateExtension("when", CloudEventAttributeType.Timestamp)] = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
                value[CloudEventAttribute.CreateExtension("where", CloudEventAttributeType.UriReference)] = new Uri("/relative", UriKind.Relative);
                value[CloudEventAttribute.CreateExtension("uri", CloudEventAttributeType.Uri)] = new Uri("https://example.test/");
                value[CloudEventAttribute.CreateExtension("label", CloudEventAttributeType.String)] = "label";
            }
            if (fixture == "full-envelope")
            {
                value.Subject = "subject"; value.Time = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero); value.DataSchema = new Uri("https://example.test/schema");
            }
        }
        return new(options, resolver, formatter, value, converter);
    }

    private static DecodeInput Input(string fixture)
    {
        var json = fixture switch
        {
            "empty-batch-decode" => "[]",
            "batch-lifetime" => "[" + Literal + "," + Literal + "]",
            "batch-literal" => "[" + Literal + "," + Prefix + ",\"data_base64\":\"AQI=\"}," + Prefix + ",\"datacontenttype\":\"text/plain\",\"data\":\"snow\"}]",
            "batch-invalid-member" => "[" + Bare + ",22]",
            "batch-nonarray" => Bare,
            "root-array" => "[]", "root-number" => "22", "root-null" => "null",
            "missing-spec" => "{\"id\":\"i\",\"source\":\"/s\",\"type\":\"t\"}",
            "null-spec" => "{\"specversion\":null}", "number-spec" => "{\"specversion\":1}", "unsupported-spec" => "{\"specversion\":\"99\"}",
            "spec03" => "{\"specversion\":\"0.3\",\"id\":\"i\",\"source\":\"/s\",\"type\":\"t\",\"data\":22}",
            "source-object" => "{\"specversion\":\"1.0\",\"id\":\"i\",\"source\":{},\"type\":\"t\"}",
            "type-bool" => "{\"specversion\":\"1.0\",\"id\":\"i\",\"source\":\"/s\",\"type\":true}",
            "time-number" => Prefix + ",\"time\":22}",
            "wrong-core-token" => "{\"specversion\":\"1.0\",\"id\":22,\"source\":\"/s\",\"type\":\"t\"}",
            "null-required" => "{\"specversion\":\"1.0\",\"id\":null,\"source\":\"/s\",\"type\":\"t\"}",
            "null-optional" => Prefix + ",\"subject\":null}", "missing-required" => "{\"specversion\":\"1.0\",\"id\":\"i\"}",
            "typed-native" => Prefix + ",\"flag\":false,\"number\":22}",
            "typed-extensions" => Prefix + ",\"flag\":\"false\",\"number\":\"22\",\"uri\":\"https://example.test/\",\"when\":\"2026-01-02T03:04:05Z\",\"binary\":\"AQI=\"}",
            "bad-typed-extension" => Prefix + ",\"number\":\"garbage\"}",
            "unknown-extensions" => Prefix + ",\"unknownflag\":false,\"unknownnumber\":22,\"unknownnull\":null}",
            "fraction" => Prefix + ",\"unknown\":1.5}", "overflow" => Prefix + ",\"unknown\":2147483648}",
            "extension-array" => Prefix + ",\"unknown\":[]}", "extension-object" => Prefix + ",\"unknown\":{}}",
            "duplicates" => Prefix + ",\"id\":\"last\",\"unknown\":\"first\",\"unknown\":null,\"data\":1,\"data\":2}",
            "duplicate-spec" => "{\"specversion\":\"0.3\",\"specversion\":\"1.0\",\"id\":\"i\",\"source\":\"/s\",\"type\":\"t\"}",
            "duplicate-base64" => Prefix + ",\"data_base64\":\"AQI=\",\"data_base64\":null}",
            "batch-utf16" or "batch-async-only" => "[" + Literal + "]",
            "batch-both-data" => "[" + Prefix + ",\"data\":1,\"data_base64\":\"AQI=\"}]",
            "batch-bad-base64" => "[" + Prefix + ",\"data_base64\":22}]",
            "both-data" => Prefix + ",\"data\":1,\"data_base64\":\"AQI=\"}",
            "data-null-base64" => Prefix + ",\"data\":null,\"data_base64\":\"AQI=\"}",
            "data-base64-null" => Prefix + ",\"data\":1,\"data_base64\":null}", "both-data-null" => Prefix + ",\"data\":null,\"data_base64\":null}",
            "base64-empty" => Prefix + ",\"data_base64\":\"\"}", "base64-malformed" => Prefix + ",\"data_base64\":\"%%%\"}", "base64-number" => Prefix + ",\"data_base64\":22}",
            "nonjson-string" => Prefix + ",\"datacontenttype\":\"application/octet-stream\",\"data\":\"text\"}",
            "nonjson-number" => Prefix + ",\"datacontenttype\":\"text/plain\",\"data\":22}", "nonjson-object" => Prefix + ",\"datacontenttype\":\"text/plain\",\"data\":{}}",
            "null-data" => Prefix + ",\"data\":null}", "array-data" => Prefix + ",\"data\":[true,22]}", "bool-data" => Prefix + ",\"data\":false}",
            "string-data" => Prefix + ",\"data\":\"text\"}", "number-data" => Prefix + ",\"data\":22}",
            "trailing-comma" or "decoder-options" => Prefix + ",}", "comments" => Prefix + ",/*fixture*/\"data\":22}",
            "depth63" => Prefix + ",\"data\":" + new string('[', 62) + "1" + new string(']', 62) + "}",
            "depth64" => Prefix + ",\"data\":" + new string('[', 63) + "1" + new string(']', 63) + "}",
            "deep" => Prefix + ",\"data\":" + new string('[', 70) + "1" + new string(']', 70) + "}",
            "malformed" or "utf16-malformed" => "{invalid", _ => Literal,
        };
        var encoding = fixture is "utf16" or "utf16-bom" or "utf16-malformed" or "batch-utf16" ? Encoding.Unicode : fixture == "utf16be" ? Encoding.BigEndianUnicode : Encoding.UTF8;
        var bytes = encoding.GetBytes(json);
        if (fixture == "position") bytes = Encoding.UTF8.GetBytes("xxx" + json);
        if (fixture == "utf16-bom") bytes = Encoding.Unicode.GetPreamble().Concat(bytes).ToArray();
        if (fixture is "utf8-bom" or "bom-conflict") bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        if (fixture == "invalid-utf8") bytes = new byte[] { (byte)'{', 0xFF, (byte)'}' };
        var media = fixture switch
        {
            "null-contenttype" => null,
            "unusual-contenttype" => new ContentType("text/not-a-cloud-event; charset=utf-8"),
            "utf16" or "utf16-bom" or "utf16-malformed" or "bom-conflict" or "batch-utf16" => new ContentType("application/cloudevents+json; charset=utf-16"),
            "utf16be" => new ContentType("application/cloudevents+json; charset=utf-16BE"),
            "unsupported-charset" => new ContentType("application/cloudevents+json; charset=not-a-real-encoding"),
            _ => new ContentType("application/cloudevents+json; charset=utf-8"),
        };
        return new(bytes, media, fixture.StartsWith("batch-", StringComparison.Ordinal) || fixture is "empty-batch-decode" or "null-batch-stream");
    }

    private static CloudEventAttribute[]? Extensions(string fixture) => fixture is "typed-extensions" or "typed-native" or "bad-typed-extension"
        ? [CloudEventAttribute.CreateExtension("flag", CloudEventAttributeType.Boolean), CloudEventAttribute.CreateExtension("number", CloudEventAttributeType.Integer), CloudEventAttribute.CreateExtension("uri", CloudEventAttributeType.Uri), CloudEventAttribute.CreateExtension("when", CloudEventAttributeType.Timestamp), CloudEventAttribute.CreateExtension("binary", CloudEventAttributeType.Binary)] : null;
    private static CloudEvent Event(object? data) => new() { Id = "literal", Source = new Uri("/source", UriKind.Relative), Type = "fixture", DataContentType = "application/json", Data = data };
    private static string Attributes(CloudEvent value) => string.Join(";", value.GetPopulatedAttributes().Select(pair => pair.Key.Name + ":" + pair.Value.GetType().FullName + ":" + pair.Key.Type.Format(pair.Value)));
    private static string EventState(CloudEvent value) => value.SpecVersion.VersionId + ";" + Attributes(value) + ";data=" + DataState(value.Data);
    private static string DataState(object? value) => value switch
    {
        null => "null", byte[] bytes => "bytes:" + Convert.ToBase64String(bytes), JsonElement json => "json:" + json.ValueKind + ":" + json.GetRawText(),
        MetadataConformancePayload payload => "payload:" + payload.Name + ":" + payload.Count, IMetadataAncestor payload => "ancestor:" + payload.Name,
        string text => "text:" + text, _ => value.GetType().FullName + ":" + value,
    };
    private static string State(Bundle bundle) => $"readonly={bundle.Options.IsReadOnly};indent={bundle.Options.WriteIndented};depth={bundle.Options.MaxDepth};comments={bundle.Options.ReadCommentHandling};trailing={bundle.Options.AllowTrailingCommas};encoder={bundle.Options.Encoder?.GetType().FullName};converter={bundle.Converter?.Writes ?? 0};event=" + (bundle.Event is null ? "null" : EventState(bundle.Event));
    private static string? Error(Action action) { try { action(); return null; } catch (Exception error) { return ErrorState(error); } }
    private static string? ErrorState(Exception? error) => error is null ? null : error.GetType().FullName + "|" + (error is ArgumentException argument ? argument.ParamName : "") + "|" + error.Message;
    private static void Match(Observation expected, Observation actual, string? context = null)
    {
        actual.Before.ShouldBe(expected.Before, customMessage: $"{context}: Before"); actual.After.ShouldBe(expected.After, customMessage: $"{context}: After"); actual.Error.ShouldBe(expected.Error, customMessage: $"{context}: Error");
        actual.Bytes.ShouldBe(expected.Bytes, customMessage: $"{context}: Bytes"); actual.ContentType.ShouldBe(expected.ContentType, customMessage: $"{context}: ContentType"); actual.Decoded.ShouldBe(expected.Decoded, customMessage: $"{context}: Decoded");
        actual.EnumerationState.ShouldBe(expected.EnumerationState, customMessage: $"{context}: EnumerationState"); actual.StreamState.ShouldBe(expected.StreamState, customMessage: $"{context}: StreamState");
    }
    private sealed record Observation(byte[]? Bytes, string ContentType, string? Error, string Before, string After, string Decoded, string EnumerationState, string StreamState);
    private sealed record DecodeInput(byte[] Bytes, ContentType? ContentType, bool Batch);
    private sealed record Bundle(JsonSerializerOptions Options, FreshMetadataResolver Resolver, CloudEventFormatter Formatter, CloudEvent? Event, RootWitnessConverter? Converter) : IDisposable
    {
        public void Dispose() { if (Event?.Data is IDisposable disposable) disposable.Dispose(); }
    }
    private sealed class FreshMetadataResolver(string fixture) : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver _context = new MetadataConformanceContext();
        public bool SawCoveredMetadata { get; private set; }
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type == typeof(MetadataConformancePayload) || type == typeof(IMetadataAncestor)) SawCoveredMetadata = true;
            return fixture == "payload-only" && type == typeof(object) ? null : _context.GetTypeInfo(type, options);
        }
    }
    private sealed class EmptyMetadataResolver : IJsonTypeInfoResolver { public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null; }
    private sealed class RootWitnessConverter(string fixture) : JsonConverter<object>
    {
        public int Writes { get; private set; }
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("write-only fixture");
        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            Writes++;
            switch (fixture)
            {
                case "converter-throw": throw new InvalidOperationException("fixture converter failure");
                case "converter-scalar": writer.WriteNumberValue(22); break;
                case "converter-array": writer.WriteStartArray(); writer.WriteNumberValue(22); writer.WriteEndArray(); break;
                case "converter-object": writer.WriteStartObject(); writer.WriteNumber("value", 22); writer.WriteEndObject(); break;
                default: writer.WriteStringValue("root-converter"); break;
            }
        }
    }
    private sealed class OneShotEvents(CloudEvent? value, string fixture) : IEnumerable<CloudEvent>
    {
        public int Enumerations { get; private set; }
        public int Moves { get; private set; }
        private readonly List<CloudEvent> _produced = [];
        private readonly List<string> _before = [];
        public string State => $"enumerations={Enumerations};moves={Moves};before=" + string.Join("|", _before) + ";after=" + string.Join("|", _produced.Select(EventState));
        private CloudEvent? Track(CloudEvent? item)
        {
            if (item is not null) { _produced.Add(item); _before.Add(EventState(item)); }
            return item;
        }
        public IEnumerator<CloudEvent> GetEnumerator()
        {
            Enumerations++; if (Enumerations != 1) throw new InvalidOperationException("enumerated twice");
            var inner = Enumerate().GetEnumerator();
            return fixture == "current-throw" ? new ThrowOnCurrent(inner) : inner;
        }
        private IEnumerable<CloudEvent> Enumerate()
        {
            if (fixture == "empty-batch") yield break;
            Moves++; yield return Track(value)!;
            if (fixture == "iterator-throw") throw new InvalidOperationException("fixture iterator failure");
            if (fixture == "null-member") { Moves++; yield return Track(null)!; }
            else if (fixture == "invalid-member") { Moves++; yield return Track(new CloudEvent())!; }
            else if (fixture is "two-batch" or "mixed-batch") { Moves++; var second = Event(new byte[] { 1, 2 }); second.Id = "second"; yield return Track(second)!; }
            if (fixture == "mixed-batch") { Moves++; var third = Event("snow"); third.Id = "third"; third.DataContentType = "text/plain"; yield return Track(third)!; }
            Moves++;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private sealed class ThrowOnCurrent(IEnumerator<CloudEvent> inner) : IEnumerator<CloudEvent>
        {
            public CloudEvent Current => throw new InvalidOperationException("fixture Current failure");
            object IEnumerator.Current => Current;
            public bool MoveNext() => inner.MoveNext();
            public void Reset() => throw new NotSupportedException();
            public void Dispose() => inner.Dispose();
        }
    }
    private sealed class NonArrayMemory(byte[] bytes) : MemoryManager<byte>
    {
        private bool _disposed;
        public override Span<byte> GetSpan() { ObjectDisposedException.ThrowIf(_disposed, this); return bytes; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) => _disposed = true;
    }
    private sealed class WitnessStream(byte[] bytes, bool nonSeekable, bool shortReads, bool asyncOnly, bool failReads, bool unreadable) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);
        private bool _disposed;
        private bool _syncRead;
        private bool _asyncRead;
        private long _consumed;
        public string State => $"disposed={_disposed};sync={_syncRead};async={_asyncRead};consumed={_consumed}";
        public override bool CanRead => !_disposed && !unreadable;
        public override bool CanSeek => !nonSeekable && !_disposed;
        public override bool CanWrite => false;
        public override long Length => nonSeekable ? throw new NotSupportedException() : _inner.Length;
        public override long Position { get => nonSeekable ? throw new NotSupportedException() : _inner.Position; set { if (nonSeekable) throw new NotSupportedException(); _inner.Position = value; } }
        public void SkipPrefix(int count) => _inner.Position = count;
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            _syncRead = true; ObjectDisposedException.ThrowIf(_disposed, this);
            if (asyncOnly || failReads) throw new IOException("fixture read failure");
            var count = _inner.Read(shortReads ? buffer[..Math.Min(buffer.Length, 3)] : buffer); _consumed += count; return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _asyncRead = true; ObjectDisposedException.ThrowIf(_disposed, this);
            if (failReads) return ValueTask.FromException<int>(new IOException("fixture read failure"));
            var count = _inner.Read(shortReads ? buffer.Span[..Math.Min(buffer.Length, 3)] : buffer.Span); _consumed += count; return ValueTask.FromResult(count);
        }
        protected override void Dispose(bool disposing) { _disposed = true; if (disposing) _inner.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => nonSeekable ? throw new NotSupportedException() : _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

internal sealed class MetadataConformancePayload
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("count")] public int Count { get; init; }
}
internal sealed class MetadataMissingPayload { public string Value { get; init; } = "missing"; }
internal interface IMetadataAncestor { string Name { get; } }
internal interface IMetadataLeft { string Name { get; } }
internal interface IMetadataRight { string Name { get; } }
internal sealed class MetadataPrivateAncestor : IMetadataAncestor { public string Name { get; init; } = string.Empty; }
internal sealed class MetadataAmbiguousAncestor : IMetadataLeft, IMetadataRight { public string Name { get; init; } = string.Empty; }
[JsonSerializable(typeof(MetadataConformancePayload))]
[JsonSerializable(typeof(IMetadataAncestor))]
[JsonSerializable(typeof(IMetadataLeft))]
[JsonSerializable(typeof(IMetadataRight))]
[JsonSerializable(typeof(int))]
internal sealed partial class MetadataConformanceContext : JsonSerializerContext;
