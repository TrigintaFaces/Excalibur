// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// Conformance arms for the JSON Event Format for CloudEvents, version 1.0.2, asserted against the
/// specification's rules and bound to <b>the path this framework actually ships</b>.
/// <para>
/// These arms deliberately exercise the formatter through
/// <see cref="CloudEventPayloadSerialization.CreateOptions"/> — a frozen, source-generated resolver
/// chain with no reflection fallback. That configuration is the strict one: a payload the resolver
/// does not cover fails here rather than being quietly rescued, so an arm that passes has passed on
/// the trim-safe path and not merely on a full runtime.
/// </para>
/// <para>
/// They are asserted against the specification rather than against its published JSON schema, because
/// the schema does not enforce the <c>data</c> / <c>data_base64</c> exclusivity rule — a schema check
/// would accept a document these arms reject.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
[Trait("Pattern", "Conformance")]
public sealed class CloudEventJsonFormatConformanceShould
{
    private static JsonEventFormatter Formatter() =>
        new(CloudEventPayloadSerialization.CreateOptions(PayloadContext.Default), default);

    private static CloudEvent MinimalEvent() => new(CloudEventsSpecVersion.V1_0)
    {
        Id = "id-1",
        Type = "com.excalibur.conformance",
        Source = new Uri("https://conformance.excalibur.test/source"),
    };

    [Fact]
    public void UseTheMediaTypeTheSpecificationRequires()
    {
        var bytes = Formatter().EncodeStructuredModeMessage(MinimalEvent(), out var contentType);

        bytes.Length.ShouldBeGreaterThan(0);
        contentType.MediaType.ShouldBe("application/cloudevents+json");
    }

    [Fact]
    public void MapOnlyBooleanToAJsonBooleanAndOnlyIntegerToAJsonNumber()
    {
        // The attribute type system maps to JSON with exactly two exceptions to "write it as a
        // string". Getting this wrong makes our events unreadable to a conformant consumer, so the arm
        // reads the JSON TOKEN TYPE -- a string "true" and a boolean true are indistinguishable by
        // text comparison.
        var flag = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
        var count = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);
        var label = CloudEventAttribute.CreateExtension("conformancelabel", CloudEventAttributeType.String);

        var cloudEvent = MinimalEvent();
        cloudEvent[flag] = true;
        cloudEvent[count] = 22;
        cloudEvent[label] = "alpha";

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));
        var root = document.RootElement;

        root.GetProperty("conformanceflag").ValueKind.ShouldBe(JsonValueKind.True);
        root.GetProperty("conformancecount").ValueKind.ShouldBe(JsonValueKind.Number);
        root.GetProperty("conformancelabel").ValueKind.ShouldBe(JsonValueKind.String);
        root.GetProperty("specversion").ValueKind.ShouldBe(JsonValueKind.String);
        root.GetProperty("source").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public void PlaceBinaryDataInDataBase64AndNeverInData()
    {
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = "application/octet-stream";
        cloudEvent.Data = new byte[] { 1, 2, 3, 250 };

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));
        var root = document.RootElement;

        root.TryGetProperty("data_base64", out var encoded).ShouldBeTrue();
        encoded.ValueKind.ShouldBe(JsonValueKind.String);
        Convert.FromBase64String(encoded.GetString()!).ShouldBe(new byte[] { 1, 2, 3, 250 });

        root.TryGetProperty("data", out _).ShouldBeFalse(
            "data and data_base64 are mutually exclusive, so emitting both would make the payload "
            + "depend on the reader's member preference");
    }

    [Fact]
    public void StoreJsonTypedDataAsANativeJsonValueRatherThanAnEncodedString()
    {
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = "application/json";
        cloudEvent.Data = new ConformancePayload { Name = "alpha", Count = 22 };

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));

        // "The data value MUST be stored directly as a JSON value, rather than as an encoded JSON
        // document represented as a string." An encoder that stringified this would still round-trip
        // through its own reader, which is why the arm asserts the token kind.
        var data = document.RootElement.GetProperty("data");
        data.ValueKind.ShouldBe(JsonValueKind.Object);
        data.GetProperty("Name").GetString().ShouldBe("alpha");
        data.GetProperty("Count").GetInt32().ShouldBe(22);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/vnd.excalibur.thing+json")]
    [InlineData("text/json; charset=utf-8")]
    public void TreatAnyJsonSuffixedMediaTypeAsJsonTyped(string dataContentType)
    {
        // "its media type, when stripped of parameters, has the form */json or */*+json". The
        // parameterised case is included because stripping parameters is a step an implementation can
        // plausibly omit, and omitting it turns a JSON payload into a quoted string.
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = dataContentType;
        cloudEvent.Data = new ConformancePayload { Name = "alpha", Count = 1 };

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));

        document.RootElement.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void TreatAnUnspecifiedContentTypeAsApplicationJson()
    {
        // "If the datacontenttype is unspecified, processing SHOULD proceed as if the datacontenttype
        // had been specified explicitly as application/json."
        var cloudEvent = MinimalEvent();
        cloudEvent.Data = new ConformancePayload { Name = "alpha", Count = 2 };

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));

        document.RootElement.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void StoreNonJsonDataAsAJsonString()
    {
        // "a JSON serializer MUST store a string representation of the data value".
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = "text/plain";
        cloudEvent.Data = "plain text body";

        using var document = JsonDocument.Parse(
            Formatter().EncodeStructuredModeMessage(cloudEvent, out _));

        var data = document.RootElement.GetProperty("data");
        data.ValueKind.ShouldBe(JsonValueKind.String);
        data.GetString().ShouldBe("plain text body");
    }

    [Fact]
    public void RefuseAnObjectPayloadUnderANonJsonContentType()
    {
        // The specification cannot be honoured here: there is no string representation of a POCO that
        // a receiver could interpret. Rendering `value.ToString()` would emit the TYPE NAME as the
        // payload -- a plausible body that is not the data, which nothing downstream can detect.
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = "application/octet-stream";
        cloudEvent.Data = new ConformancePayload { Name = "alpha", Count = 3 };

        _ = Should.Throw<ArgumentException>(() => Formatter().EncodeStructuredModeMessage(cloudEvent, out _));

    }

    [Fact]
    public void RefuseADocumentCarryingBothDataAndDataBase64()
    {
        // THE ARM THE SPECIFICATION'S OWN SCHEMA CANNOT PROVIDE. A schema validator accepts this
        // document; the specification forbids it. Accepting it and preferring one member would make
        // the payload depend on an implementation detail rather than on the sender's intent.
        _ = Should.Throw<ArgumentException>(() => Formatter().DecodeStructuredModeMessage(
            Encoding.UTF8.GetBytes("""
                {
                  "specversion": "1.0",
                  "id": "id-1",
                  "type": "com.excalibur.conformance",
                  "source": "https://conformance.excalibur.test/source",
                  "data": { "Name": "alpha", "Count": 1 },
                  "data_base64": "AQIDew=="
                }
                """),
            contentType: null,
            extensionAttributes: null));

    }

    [Theory]
    [InlineData("\"9.9\"", "an unrecognised version")]
    [InlineData("\"0.3\"", "a real but unsupported earlier version")]
    public void RefuseADocumentWhoseSpecVersionItCannotHonour(string specVersionJson, string why)
    {
        // specversion SELECTS THE ATTRIBUTE SET, so a value that cannot be honoured means every other
        // member is being interpreted against rules the sender did not choose. Defaulting to 1.0 would
        // decode a stranger's document against our assumption and tell nothing downstream -- silence,
        // not severity, which is the discriminator this project treats as catastrophic.
        var document = $$"""
            {
              "specversion": {{specVersionJson}},
              "id": "id-1",
              "type": "com.excalibur.conformance",
              "source": "https://conformance.excalibur.test/source"
            }
            """;

        _ = Should.Throw<ArgumentException>(
            () => Formatter().DecodeStructuredModeMessage(
                Encoding.UTF8.GetBytes(document), contentType: null, extensionAttributes: null),
            $"a document carrying {why} must be refused, not decoded against the 1.0 attribute set");

    }

    [Fact]
    public void AcceptTheSpecVersionItDoesSupport()
    {
        // LIVENESS for the refusals above. Without this arm they are all satisfied by a decoder that
        // rejects every document it is ever given.
        var decoded = Formatter().DecodeStructuredModeMessage(
            Encoding.UTF8.GetBytes("""
                {
                  "specversion": "1.0",
                  "id": "id-1",
                  "type": "com.excalibur.conformance",
                  "source": "https://conformance.excalibur.test/source"
                }
                """),
            contentType: null,
            extensionAttributes: null);

        decoded.SpecVersion.ShouldBe(CloudEventsSpecVersion.V1_0);
        decoded.Id.ShouldBe("id-1");
    }

    [Fact]
    public void RoundTripAttributesThroughEncodeAndDecode()
    {
        var flag = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
        var count = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);

        var original = MinimalEvent();
        original.DataContentType = "application/json";
        original[flag] = true;
        original[count] = 7;
        original.Data = new ConformancePayload { Name = "alpha", Count = 7 };

        var bytes = Formatter().EncodeStructuredModeMessage(original, out _);
        var decoded = Formatter().DecodeStructuredModeMessage(bytes, null, [flag, count]);

        decoded.Id.ShouldBe("id-1");
        decoded.Type.ShouldBe("com.excalibur.conformance");
        decoded.Source.ShouldBe(new Uri("https://conformance.excalibur.test/source"));
        decoded.SpecVersion.ShouldBe(CloudEventsSpecVersion.V1_0);
        decoded[flag].ShouldBe(true);
        decoded[count].ShouldBe(7);
        decoded.Data.ShouldNotBeNull();
    }

    /// <summary>
    /// Decodes <paramref name="document"/> and reports the <c>id</c> that survived, or the refusal.
    /// </summary>
    /// <remarks>
    /// Two outcomes are acceptable for a hostile document — refuse it, or read it correctly. Only one
    /// is not: silently adopting a value the attacker nested somewhere the parser was not supposed to
    /// look. So these arms assert on the <i>surviving id</i> rather than on an exception type, because
    /// pinning the exception type would make a refusal the only pass and an equally-safe correct parse
    /// a failure.
    /// </remarks>
    private static string SurvivingId(string document)
    {
        try
        {
            return Formatter().DecodeStructuredModeMessage(
                Encoding.UTF8.GetBytes(document), contentType: null, extensionAttributes: null).Id
                ?? "<null>";
        }
        catch (Exception ex) when (ex is not ShouldAssertException)
        {
            return "<refused>";
        }
    }

    [Theory]
    [InlineData(
        """{"specversion":"1.0","specversion":{"id":"INJECTED"},"id":"real","type":"t","source":"/s"}""",
        "a duplicate specversion whose second value is an object")]
    [InlineData(
        """{"specversion":"1.0","specversion":[1,2],"id":"real","type":"t","source":"/s"}""",
        "a duplicate specversion whose second value is an array")]
    [InlineData(
        """{"specversion":"1.0","specversion":{"id":"INJECTED"},"id":"real","type":"t","source":"/s"}""",
        "an ESCAPED duplicate spelling, which unescapes to the same member name")]
    public void NotAdoptAnAttributeNestedInsideADuplicateSpecVersionMember(string document, string shape)
    {
        // JSON permits duplicate member names (RFC 8259 says names SHOULD be unique, not MUST). A
        // decoder that answers "what is the FIRST specversion?" in one pass and "what is EVERY
        // member?" in another can be made to disagree with itself: the first pass accepts a valid
        // string and stops, and the second descends into a composite duplicate and reads the
        // attacker's nested members as top-level attributes.
        //
        // This is measured against the SHIPPED decode path, because that is the one a consumer runs.
        SurvivingId(document).ShouldNotBe(
            "INJECTED",
            $"{shape} must not be able to choose this event's id");
    }

    [Fact]
    public void NotLetADuplicateSpecVersionSmuggleDataBase64PastTheExclusivityRule()
    {
        // The nastier half of the same class, because it defeats a CHECK rather than merely
        // truncating the document: if `data_base64` is seen from inside a nested member while the real
        // `data` is seen at the top level, a decoder tracking "did I see both?" can conclude it saw
        // only one -- and then silently prefer the attacker's bytes as the payload.
        const string Smuggled =
            """{"specversion":"1.0","specversion":{"data_base64":"AQIDew=="},"id":"real","type":"t","source":"/s","data":{"Name":"alpha"}}""";

        string verdict;
        try
        {
            var decoded = Formatter().DecodeStructuredModeMessage(
                Encoding.UTF8.GetBytes(Smuggled), contentType: null, extensionAttributes: null);
            verdict = decoded.Data switch
            {
                byte[] => "ADOPTED-SMUGGLED-BYTES",
                null => "<null payload>",
                _ => "kept the real data member",
            };
        }
        catch (Exception ex) when (ex is not ShouldAssertException)
        {
            verdict = "<refused>";
        }

        verdict.ShouldNotBe(
            "ADOPTED-SMUGGLED-BYTES",
            "a payload nested inside a duplicate member must not replace the document's real data");
    }

    [Theory]
    [InlineData(
        """{"specversion":"1.0","specversion":"0.3","id":"real","type":"t","source":"/s"}""",
        "<refused>",
        "the SECOND specversion is unsupported, so honouring it means refusing; a decoder that "
        + "honoured the FIRST would decode this as 1.0")]
    [InlineData(
        """{"specversion":"0.3","specversion":"1.0","id":"real","type":"t","source":"/s"}""",
        "real",
        "the SECOND specversion is supported, so honouring it means decoding; a decoder that "
        + "honoured the FIRST would refuse this as unsupported")]
    public void HonourTheSameDuplicateInBothTheSpecVersionLookupAndTheAttributeScan(
        string document, string expected, string why)
    {
        // THE LOAD-BEARING PREDICATE, and it is not "hostile documents are refused".
        //
        // The injection class that broke this framework's own hand-written decoder lived in a
        // DISAGREEMENT between two scans of the same byte string: one asked "what is the FIRST
        // specversion?" and stopped, the other asked "what is EVERY member?" and kept going, so a
        // duplicate member let an attacker be read differently by each. The property that kills the
        // whole class is that the two reads agree about which duplicate wins.
        //
        // These two cases pin that two-sided, which is what makes them a measurement: each one's
        // expected result INVERTS under a first-wins rule, so they cannot both pass unless the rule is
        // consistent. An arm asserting only that a nested value did not survive would stay green if a
        // future package version made the specversion lookup first-wins while the attribute scan
        // stayed last-wins -- and that is precisely the configuration in which the injection reopens.
        SurvivingId(document).ShouldBe(expected, why);
    }

    [Fact]
    public void ApplyTheLastDuplicateWhenAnOrdinaryAttributeIsRepeated()
    {
        // The same resolution rule observed directly on an ordinary attribute, so the arm above is
        // pinning a GENERAL rule rather than a quirk of the specversion lookup.
        SurvivingId("""{"specversion":"1.0","id":"first","id":"second","type":"t","source":"/s"}""")
            .ShouldBe("second");
    }

    [Fact]
    public void NotSilentlyIgnoreContentTrailingTheJsonObject()
    {
        // A decoder that stops at the first closing brace without asserting end-of-document accepts a
        // byte string that a different decoder reads differently. Two decoders disagreeing about one
        // byte string is how a message gets past a filter, so the trailing content must be refused
        // rather than discarded.
        const string Trailing =
            """{"specversion":"1.0","id":"real","type":"t","source":"/s"}{"id":"INJECTED"}""";

        SurvivingId(Trailing).ShouldBe(
            "<refused>",
            "content after the JSON object must be refused, not silently discarded");
    }

    [Fact]
    public void StillDecodeAWellFormedDocumentWithADuplicateScalarMember()
    {
        // LIVENESS, and it is the control that makes the three arms above measurements rather than
        // stories: the same duplicate SHAPE with a scalar second value is harmless and must still
        // decode. Without this arm, all of the above pass against a decoder that refuses everything.
        // Asserted as EXACTLY "real" rather than "real or refused". The permissive version passes
        // against a decoder that refuses every document, which would make all four safety arms above
        // vacuous -- they assert only that "INJECTED" did not survive, and nothing survives a decoder
        // that always throws. This arm is the one that proves the others measured something.
        SurvivingId(
            """{"specversion":"1.0","specversion":"1.0","id":"real","type":"t","source":"/s"}""")
            .ShouldBe("real");
    }

    [Fact]
    public void ReEncodeAnEventItJustDecoded()
    {
        // THE FORWARDING PATH, and the one that was broken until JsonElement was declared in the
        // framework's own serializer context. Decode yields a JsonElement payload, so re-encoding asks
        // the resolver for JsonElement metadata -- and a resolver chain has NO reflection fallback, so
        // the trim-safe configuration was the one that failed. A transport that receives an event and
        // republishes it is the ordinary case, not an exotic one.
        var inbound = Encoding.UTF8.GetBytes("""
            {
              "specversion": "1.0",
              "id": "id-1",
              "type": "com.excalibur.conformance",
              "source": "https://conformance.excalibur.test/source",
              "datacontenttype": "application/json",
              "data": { "Name": "alpha", "Count": 22 }
            }
            """);

        var formatter = Formatter();
        var decoded = formatter.DecodeStructuredModeMessage(inbound, null, null);

        using var document = JsonDocument.Parse(formatter.EncodeStructuredModeMessage(decoded, out _));

        var data = document.RootElement.GetProperty("data");
        data.ValueKind.ShouldBe(
            JsonValueKind.Object,
            "a forwarded payload must stay a native JSON value rather than becoming a quoted string");
        data.GetProperty("Name").GetString().ShouldBe("alpha");
    }

    [Fact]
    public void EncodeABinaryModeBodyAsTheBytesThemselvesRatherThanBase64()
    {
        // Base64 is a JSON-FORMAT marshaling rule for data_base64. A binary-mode body is the bytes.
        // Conflating the two double-encodes every binary-mode payload, and because both are
        // valid-looking byte arrays nothing downstream reports it.
        var cloudEvent = MinimalEvent();
        cloudEvent.DataContentType = "application/octet-stream";
        cloudEvent.Data = new byte[] { 9, 8, 7 };

        Formatter().EncodeBinaryModeEventData(cloudEvent).ToArray().ShouldBe(new byte[] { 9, 8, 7 });
    }
}
