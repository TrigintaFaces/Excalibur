// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

/// <summary>Separates the recorded literal upstream byte criterion from payload semantics.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Conformance")]
public sealed class SharedCloudEventUpstreamOracleShould
{
    [Theory]
    [InlineData("minimal")]
    [InlineData("plain-text")]
    [InlineData("binary")]
    [InlineData("empty-binary")]
    [InlineData("primitive-extensions")]
    [InlineData("other-extensions")]
    [InlineData("utc-time")]
    [InlineData("offset-time")]
    [InlineData("different-attribute-order")]
    [InlineData("escaped-source")]
    [InlineData("unspecified-content-type")]
    public async Task MatchPinnedUpstreamStructuredEnvelopeBytes(string scenario)
    {
        var expected = Formatter().EncodeStructuredModeMessage(Event(scenario), out var contentType);

        var actual = await EncodeAsync(Event(scenario));

        new ContentType(actual.ContentType!).MediaType.ShouldBe(contentType.MediaType);
        actual.Body.ToArray().ShouldBe(expected.ToArray(),
            "the recorded mvx3f0 byte-for-byte criterion is literal; JSON normalization must not hide a difference");
    }

    [Theory]
    [InlineData("{\"Count\":22}")]
    [InlineData("[false,22,\"x\"]")]
    [InlineData("true")]
    [InlineData("22")]
    [InlineData("null")]
    public async Task PreserveUpstreamNativeJsonPayloadValuesWithoutReflection(string json)
    {
        var expected = Event("minimal");
        expected.DataContentType = "application/json";
        expected.Data = JsonValue(json);
        using var oracle = JsonDocument.Parse(Formatter().EncodeStructuredModeMessage(expected, out _));
        var original = Event("minimal");
        original.DataContentType = "application/json";
        original.Data = JsonValue(json);

        var encoded = await EncodeAsync(original);

        using var actual = JsonDocument.Parse(encoded.Body);
        actual.RootElement.TryGetProperty("data_base64", out _).ShouldBeFalse();
        actual.RootElement.TryGetProperty("data", out var data).ShouldBeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(oracle.RootElement.GetProperty("data").GetRawText()),
            JsonNode.Parse(data.GetRawText())).ShouldBeTrue("native JSON values must stay native JSON values");
    }

    [Fact]
    public async Task PreserveAnExplicitBinaryPayloadEvenWhenItsMediaTypeSaysJson()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"Count\":22}");
        var original = Event("minimal");
        original.DataContentType = "application/json";
        original.Data = bytes;
        using var oracle = JsonDocument.Parse(Formatter().EncodeStructuredModeMessage(original, out _));

        var encoded = await EncodeAsync(original);

        using var actual = JsonDocument.Parse(encoded.Body);
        actual.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
        actual.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(bytes);
        oracle.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(bytes);
    }

    [Fact]
    public async Task PreserveOnlyTheSelectedMemoryBytesUsingTheUpstreamBinaryRepresentation()
    {
        var bytes = new byte[] { 9, 0, 1, 254, 255, 8 };
        var slice = new ReadOnlyMemory<byte>(bytes, 1, 4);
        var original = Event("minimal");
        original.DataContentType = "application/octet-stream";
        original.Data = slice;
        var upstreamBinary = Event("minimal");
        upstreamBinary.DataContentType = original.DataContentType;
        upstreamBinary.Data = slice.ToArray();
        using var oracle = JsonDocument.Parse(Formatter().EncodeStructuredModeMessage(upstreamBinary, out _));

        var encoded = await EncodeAsync(original);

        using var actual = JsonDocument.Parse(encoded.Body);
        actual.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
        actual.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(slice.ToArray());
        oracle.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(slice.ToArray());
    }

    [Fact]
    public async Task PreserveNativeJsonMeaningWhenForwardingAPubliclyDecodedEvent()
    {
        const string wire = """
            {"specversion":"1.0","id":"forwarded","source":"/origin","type":"conformance.forward","datacontenttype":"application/json","data":{"Count":22}}
            """;
        var received = new TransportReceivedMessage
        {
            Id = "broker-receipt",
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes(wire),
        };
        await using var receiver = new RecordingReceiver(received).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);
        (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem().ShouldBeSameAs(received);
        received.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        var decoded = received.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        var upstreamDecoded = Formatter().DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(wire), null, null);
        using var oracle = JsonDocument.Parse(Formatter().EncodeStructuredModeMessage(upstreamDecoded, out _));

        var encoded = await EncodeAsync(decoded);

        using var actual = JsonDocument.Parse(encoded.Body);
        actual.RootElement.GetProperty("id").GetString().ShouldBe("forwarded");
        actual.RootElement.TryGetProperty("data_base64", out _).ShouldBeFalse(
            "decoding and forwarding JSON must not change its logical data type into binary");
        JsonNode.DeepEquals(JsonNode.Parse(oracle.RootElement.GetProperty("data").GetRawText()),
            JsonNode.Parse(actual.RootElement.GetProperty("data").GetRawText())).ShouldBeTrue();
        Encoding.UTF8.GetString(received.Body.Span).ShouldBe(wire);
    }

    private static JsonEventFormatter Formatter() => new(UpstreamEnvelopeOracleContext.Default.Options, default);

    private static JsonElement JsonValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CloudEvent Event(string scenario)
    {
        var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0);
        if (scenario == "different-attribute-order")
        {
            cloudEvent.Type = "conformance.oracle";
            cloudEvent.Source = new Uri("https://example.test/origin");
            cloudEvent.Id = "oracle-id";
        }
        else
        {
            cloudEvent.Id = "oracle-id";
            cloudEvent.Type = "conformance.oracle";
            cloudEvent.Source = new Uri("https://example.test/origin");
        }

        switch (scenario)
        {
            case "minimal":
            case "different-attribute-order":
                break;
            case "plain-text":
                cloudEvent.DataContentType = "text/plain";
                cloudEvent.Data = "quoted \"text\" \\ \u03bb\U0001f642";
                break;
            case "binary":
                cloudEvent.DataContentType = "application/octet-stream";
                cloudEvent.Data = new byte[] { 0, 1, 254, 255 };
                break;
            case "empty-binary":
                cloudEvent.DataContentType = "application/octet-stream";
                cloudEvent.Data = Array.Empty<byte>();
                break;
            case "primitive-extensions":
                cloudEvent[CloudEventAttribute.CreateExtension("flag", CloudEventAttributeType.Boolean)] = false;
                cloudEvent[CloudEventAttribute.CreateExtension("count", CloudEventAttributeType.Integer)] = int.MinValue;
                cloudEvent[CloudEventAttribute.CreateExtension("label", CloudEventAttributeType.String)] = "22";
                break;
            case "other-extensions":
                cloudEvent[CloudEventAttribute.CreateExtension("binaryvalue", CloudEventAttributeType.Binary)] = new byte[] { 0, 255 };
                cloudEvent[CloudEventAttribute.CreateExtension("urivalue", CloudEventAttributeType.Uri)] = new Uri("https://example.test/a%2Fb");
                cloudEvent[CloudEventAttribute.CreateExtension("referencevalue", CloudEventAttributeType.UriReference)] = new Uri("../a%2Fb", UriKind.Relative);
                cloudEvent[CloudEventAttribute.CreateExtension("stampvalue", CloudEventAttributeType.Timestamp)] = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
                break;
            case "utc-time":
                cloudEvent.Time = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
                break;
            case "offset-time":
                cloudEvent.Time = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.FromHours(5.5));
                break;
            case "escaped-source":
                cloudEvent.Source = new Uri("https://example.test/origin?q=%E9%9B%AA");
                break;
            case "unspecified-content-type":
                cloudEvent.Data = "payload";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return cloudEvent;
    }

    private static async Task<TransportMessage> EncodeAsync(CloudEvent cloudEvent)
    {
        await using var inner = new RecordingSender();
        await inner.WithCloudEventEncoding().SendAsync(TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);
        return inner.Sent.ShouldHaveSingleItem();
    }

    private sealed class RecordingSender : ITransportSender
    {
        public List<TransportMessage> Sent { get; } = [];

        public string Destination => "upstream-envelope-oracle";

        public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(message);
            return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
        }

        public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This oracle compares individual structured envelopes.");

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingReceiver(TransportReceivedMessage message) : ITransportReceiver
    {
        public string Source => "upstream-forwarding-oracle";

        public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([message]);
        }

        public Task AcknowledgeAsync(TransportReceivedMessage received, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RejectAsync(TransportReceivedMessage received, string? reason, bool requeue, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This oracle does not reject receipts.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(byte[]))]
internal sealed partial class UpstreamEnvelopeOracleContext : JsonSerializerContext;
