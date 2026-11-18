// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class SharedCloudEventJsonMediaTypeShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/problem+json; charset=utf-8")]
    [InlineData("text/example+json; profile=example")]
    public async Task PreserveJsonValuesWithAbsentOrDeclaredJsonMediaTypes(string? contentType)
    {
        using var data = JsonDocument.Parse("{\"Count\":22}");
        var cloudEvent = Event(data.RootElement, contentType);
        var expected = Formatter().EncodeStructuredModeMessage(cloudEvent, out _);
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();

        await sender.SendAsync(TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);

        var encoded = inner.Sent.ShouldHaveSingleItem();
        encoded.Body.ToArray().ShouldBe(expected.ToArray());
        var received = Message(encoded.Body);
        await using var receiver = new RecordingReceiver([received]).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);
        (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem().ShouldBeSameAs(received);
        received.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        var decoded = received.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        decoded.Data.ShouldBeOfType<JsonElement>().GetProperty("Count").GetInt32().ShouldBe(22);
    }

    [Fact]
    public async Task RefuseEmptyMediaTypeAtThePublicSdkSetterAndPermitCorrectedSameMessageRetry()
    {
        using var data = JsonDocument.Parse("{\"Count\":22}");
        var cloudEvent = Event(data.RootElement, "application/problem+json");
        var message = TransportMessage.FromString("original").WithCloudEvent(cloudEvent);
        message.ContentType = "text/plain";
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();

        Should.Throw<ArgumentException>(() => cloudEvent.DataContentType = string.Empty);

        cloudEvent.DataContentType.ShouldBe("application/problem+json");
        inner.Sent.ShouldBeEmpty();
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("original");
        message.ContentType.ShouldBe("text/plain");
        message.Properties.Values.ShouldContain(value => ReferenceEquals(value, cloudEvent));
        cloudEvent.DataContentType = "application/json";
        await sender.SendAsync(message, CancellationToken.None);
        inner.Sent.ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.Body.ToArray().ShouldBe(Formatter().EncodeStructuredModeMessage(cloudEvent, out _).ToArray());

        var corrected = Message(message.Body);
        var healthyEvent = Event(data.RootElement, "application/json");
        healthyEvent.Id = "healthy-after-sdk-refusal";
        var healthy = Message(Formatter().EncodeStructuredModeMessage(healthyEvent, out _));
        await using var receiverInner = new RecordingReceiver([corrected, healthy]);
        await using var receiver = receiverInner.WithCloudEventDecoding(CloudEventBinding.StructuredOnly);
        var batch = await receiver.ReceiveAsync(2, CancellationToken.None);
        batch.Count.ShouldBe(2);
        batch[0].ShouldBeSameAs(corrected);
        batch[1].ShouldBeSameAs(healthy);
        foreach (var received in batch)
        {
            received.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
            received.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem().Data
                .ShouldBeOfType<JsonElement>().GetProperty("Count").GetInt32().ShouldBe(22);
        }

        healthy.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem().Id.ShouldBe("healthy-after-sdk-refusal");
        receiverInner.Settlements.ShouldBe(0);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("application/octet-stream")]
    public async Task RefuseNonJsonElementMediaTypesBeforeIoAndPermitCorrectedSameMessageRetry(string contentType)
    {
        using var data = JsonDocument.Parse("{\"Count\":22}");
        var cloudEvent = Event(data.RootElement, contentType);
        Should.Throw<ArgumentException>(() => Formatter().EncodeStructuredModeMessage(cloudEvent, out _));
        var message = TransportMessage.FromString("original").WithCloudEvent(cloudEvent);
        message.ContentType = "text/plain";
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();

        await Should.ThrowAsync<NotSupportedException>(() => sender.SendAsync(message, CancellationToken.None));

        inner.Sent.ShouldBeEmpty();
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("original");
        message.ContentType.ShouldBe("text/plain");
        message.Properties.Values.ShouldContain(value => ReferenceEquals(value, cloudEvent));
        cloudEvent.DataContentType = "application/json";
        await sender.SendAsync(message, CancellationToken.None);
        inner.Sent.ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.Body.ToArray().ShouldBe(Formatter().EncodeStructuredModeMessage(cloudEvent, out _).ToArray());
    }

    [Theory]
    [InlineData("{\"Count\":22}", "text/plain")]
    [InlineData("{\"Count\":22}", "")]
    [InlineData("[22]", "text/plain")]
    [InlineData("true", "text/plain")]
    [InlineData("22", "text/plain")]
    [InlineData("null", "text/plain")]
    public async Task AnnotateMessageDerivedNonJsonNonStringDataAndStillDecodeTheNextMessage(string dataJson, string contentType)
    {
        var wire = $$"""
            {"specversion":"1.0","id":"poison","type":"conformance.media","source":"/origin","datacontenttype":"{{contentType}}","data":{{dataJson}}}
            """;
        var poison = Message(Encoding.UTF8.GetBytes(wire));
        const string healthyWire = """
            {"specversion":"1.0","id":"healthy","type":"conformance.media","source":"/origin","datacontenttype":"application/json","data":{"Count":22}}
            """;
        var healthy = Message(Encoding.UTF8.GetBytes(healthyWire));
        await using var inner = new RecordingReceiver([poison, healthy]);
        await using var receiver = inner.WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        var received = await receiver.ReceiveAsync(2, CancellationToken.None);

        received.Count.ShouldBe(2);
        received[0].ShouldBeSameAs(poison);
        received[1].ShouldBeSameAs(healthy);
        poison.ProviderData.Values.OfType<InvalidOperationException>().ShouldHaveSingleItem();
        poison.ProviderData.Values.OfType<CloudEvent>().ShouldBeEmpty();
        healthy.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        healthy.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem().Data
            .ShouldBeOfType<JsonElement>().GetProperty("Count").GetInt32().ShouldBe(22);
        Encoding.UTF8.GetString(poison.Body.Span).ShouldBe(wire);
        Encoding.UTF8.GetString(healthy.Body.Span).ShouldBe(healthyWire);
        inner.Settlements.ShouldBe(0);
    }

    [Theory]
    [InlineData("text/plain", false)]
    [InlineData("text/plain", true)]
    [InlineData("application/json", false)]
    [InlineData("application/problem+json; charset=utf-8", true)]
    public async Task KeepExplicitBinaryIndependentOfDeclaredDataMediaType(string contentType, bool memorySlice)
    {
        var backing = new byte[] { 9, 0, 1, 254, 255, 8 };
        var expected = backing.AsSpan(1, 4).ToArray();
        object payload = memorySlice ? (object)new ReadOnlyMemory<byte>(backing, 1, 4) : expected;
        if (memorySlice)
        {
            payload.ShouldBeOfType<ReadOnlyMemory<byte>>();
        }
        else
        {
            payload.ShouldBeOfType<byte[]>();
        }

        var cloudEvent = Event(payload, contentType);
        var oracle = Formatter().EncodeStructuredModeMessage(Event(expected, contentType), out _);
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();
        await sender.SendAsync(TransportMessage.FromString("original").WithCloudEvent(cloudEvent), CancellationToken.None);
        var encoded = inner.Sent.ShouldHaveSingleItem();
        encoded.Body.ToArray().ShouldBe(oracle.ToArray());
        using var document = JsonDocument.Parse(encoded.Body);
        document.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
        document.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(expected);
        var received = Message(encoded.Body);
        await using var receiver = new RecordingReceiver([received]).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);
        _ = await receiver.ReceiveAsync(1, CancellationToken.None);
        received.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem().Data.ShouldBeOfType<byte[]>().ShouldBe(expected);
        backing.ShouldBe(new byte[] { 9, 0, 1, 254, 255, 8 });
    }

    private static CloudEvent Event(object data, string? contentType) => new(CloudEventsSpecVersion.V1_0)
    {
        Id = "media-type",
        Type = "conformance.media",
        Source = new Uri("https://example.test/media"),
        DataContentType = contentType,
        Data = data,
    };

    private static TransportReceivedMessage Message(ReadOnlyMemory<byte> body) => new()
    {
        Id = "broker-receipt",
        ContentType = "application/cloudevents+json",
        Body = body,
    };

    private static JsonEventFormatter Formatter() => new(UpstreamEnvelopeOracleContext.Default.Options, default);

    private sealed class RecordingReceiver(IReadOnlyList<TransportReceivedMessage> messages) : ITransportReceiver
    {
        public string Source => "media-type-controls";

        public int Settlements { get; private set; }

        public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(messages);
        }

        public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
        {
            Settlements++;
            return Task.CompletedTask;
        }

        public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
        {
            Settlements++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingSender : ITransportSender
    {
        public List<TransportMessage> Sent { get; } = [];

        public string Destination => "media-type-controls";

        public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(message);
            return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
        }

        public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Individual public sends exercise media type admission.");

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}