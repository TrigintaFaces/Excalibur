// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

/// <summary>Checks extension preservation through the public shared send and receive paths.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Conformance")]
public sealed class StructuredCloudEventRoundTripShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveBooleanExtension(bool value)
    {
        var attribute = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
        var original = CreateEvent();
        original[attribute] = value;

        var actual = await RoundTripAsync(original);

        ReadExtension(actual, attribute.Name).ShouldBeOfType<bool>().ShouldBe(value);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task PreserveIntegerExtension(int value)
    {
        var attribute = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);
        var original = CreateEvent();
        original[attribute] = value;

        var actual = await RoundTripAsync(original);

        ReadExtension(actual, attribute.Name).ShouldBeOfType<int>().ShouldBe(value);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("22")]
    [InlineData("snow-\u96ea")]
    public async Task PreserveStringWithoutInferringItsType(string value)
    {
        var attribute = CloudEventAttribute.CreateExtension("conformancelabel", CloudEventAttributeType.String);
        var original = CreateEvent();
        original[attribute] = value;

        var actual = await RoundTripAsync(original);

        ReadExtension(actual, attribute.Name).ShouldBeOfType<string>().ShouldBe(value);
    }

    [Fact]
    public async Task DecodeIndependentlyWrittenTypedExtensions()
    {
        const string wire = """
            {"specversion":"1.0","id":"independent-wire","source":"https://example.test/origin","type":"com.example.roundtrip","data":"payload","conformanceflag":false,"conformancecount":22,"conformancelabel":"22"}
            """;
        var received = new TransportReceivedMessage
        {
            Id = "independent-broker-envelope",
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes(wire),
        };
        var inner = new RecordingReceiver([received]);
        await using var receiver = inner.WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        var returned = (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem();

        returned.ShouldBeSameAs(received);
        var actual = returned.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        actual.Id.ShouldBe("independent-wire");
        actual.Source.ShouldBe(new Uri("https://example.test/origin"));
        actual.Type.ShouldBe("com.example.roundtrip");
        actual.Data.ShouldBe("payload");
        ReadExtension(actual, "conformanceflag").ShouldBeOfType<bool>().ShouldBeFalse();
        ReadExtension(actual, "conformancecount").ShouldBeOfType<int>().ShouldBe(22);
        ReadExtension(actual, "conformancelabel").ShouldBeOfType<string>().ShouldBe("22");
    }

    [Fact]
    public async Task KeepAnExtensionFreeCloudEventDecodable() =>
        (await RoundTripAsync(CreateEvent())).Id.ShouldBe("roundtrip-source");

    [Fact]
    public async Task LeaveOrdinaryMessagesAndSettlementUntouched()
    {
        var message = new TransportReceivedMessage
        {
            Id = "ordinary-broker-envelope",
            ContentType = "text/plain",
            Body = Encoding.UTF8.GetBytes("ordinary"),
        };
        var inner = new RecordingReceiver([message]);
        await using var receiver = inner.WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.ProviderData.ShouldBeEmpty();
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("ordinary");
        await receiver.AcknowledgeAsync(message, CancellationToken.None);
        inner.Acknowledged.ShouldHaveSingleItem().ShouldBeSameAs(message);
    }

    [Fact]
    public async Task TreatNullExtensionsAsOmitted()
    {
        const string body = """
            {"specversion":"1.0","id":"null-extension","source":"/origin","type":"com.example.roundtrip","subject":null,"optionalvalue":null,"data":"payload"}
            """;
        var message = new TransportReceivedMessage
        {
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes(body),
        };
        await using var receiver = new RecordingReceiver([message]).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem().ShouldBeSameAs(message);

        message.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        var actual = message.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        actual.Subject.ShouldBeNull();
        actual.GetPopulatedAttributes().ShouldNotContain(pair => pair.Key.Name == "optionalvalue");
        actual.Data.ShouldBe("payload");
    }

    [Theory]
    [InlineData("BadExtension", "true", true)]
    [InlineData("conformancecount", "2147483648", false)]
    [InlineData("conformancecount", "1.5", false)]
    public async Task IsolateMalformedExtensionsWithoutLosingTheHealthyMessage(string name, string jsonValue, bool invalidName)
    {
        var body = "{\"specversion\":\"1.0\",\"id\":\"invalid-extension\",\"source\":\"/origin\",\"type\":\"com.example.roundtrip\",\""
            + name + "\":" + jsonValue + "}";
        var malformed = new TransportReceivedMessage
        {
            Id = "original-malformed-receipt",
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes(body),
        };
        var healthy = new TransportReceivedMessage
        {
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"healthy\",\"source\":\"/origin\",\"type\":\"com.example.roundtrip\",\"data\":\"payload\"}"),
        };
        await using var receiver = new RecordingReceiver([malformed, healthy]).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        var returned = await receiver.ReceiveAsync(2, CancellationToken.None);

        returned.Count.ShouldBe(2);
        returned[0].ShouldBeSameAs(malformed);
        returned[1].ShouldBeSameAs(healthy);
        Encoding.UTF8.GetString(malformed.Body.Span).ShouldBe(body);
        malformed.ProviderData.Values.OfType<CloudEvent>().ShouldBeEmpty();
        var error = malformed.ProviderData.Values.OfType<Exception>().ShouldHaveSingleItem();
        if (invalidName)
        {
            error.ShouldBeOfType<InvalidOperationException>().InnerException.ShouldBeOfType<ArgumentException>();
        }
        else
        {
            error.ShouldBeOfType<FormatException>();
        }

        healthy.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        healthy.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem().Id.ShouldBe("healthy");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task PreserveCoreAndRawUnknownSecondaryMappings(string secondaryJson)
    {
        var body = "{\"specversion\":\"1.0\",\"id\":\"secondary-mapping\",\"source\":\"/origin\",\"type\":\"com.example.roundtrip\",\"data\":\"payload\",\"secondarymapped\":"
            + secondaryJson + "}";
        var message = new TransportReceivedMessage
        {
            ContentType = "application/cloudevents+json",
            Body = Encoding.UTF8.GetBytes(body),
        };
        await using var receiver = new RecordingReceiver([message]).WithCloudEventDecoding(CloudEventBinding.StructuredOnly);

        (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem().ShouldBeSameAs(message);

        Encoding.UTF8.GetString(message.Body.Span).ShouldBe(body);
        message.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        var actual = message.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        actual.Id.ShouldBe("secondary-mapping");
        actual.Data.ShouldBe("payload");
    }

    private static object ReadExtension(CloudEvent cloudEvent, string name) =>
        cloudEvent.GetPopulatedAttributes().Where(pair => pair.Key.Name == name)
            .ShouldHaveSingleItem($"valid extension '{name}' must survive public decoding").Value;

    private static CloudEvent CreateEvent() => new()
    {
        Id = "roundtrip-source",
        Type = "com.example.roundtrip",
        Source = new Uri("https://example.test/origin"),
        Subject = "subject-22",
        Time = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
        DataSchema = new Uri("https://example.test/schema"),
        DataContentType = "text/plain",
        Data = "payload",
    };

    private static async Task<CloudEvent> RoundTripAsync(CloudEvent original)
    {
        var sender = new RecordingSender();
        await sender.WithCloudEventEncoding().SendAsync(
            TransportMessage.FromString("unencoded").WithCloudEvent(original), CancellationToken.None);
        var encoded = sender.Sent.ShouldHaveSingleItem();
        var body = encoded.Body.ToArray();
        var settlement = new object();
        var received = new TransportReceivedMessage
        {
            Id = "distinct-broker-envelope",
            Body = body,
            ContentType = encoded.ContentType,
            Properties = new Dictionary<string, object>(StringComparer.Ordinal) { ["applicationkey"] = "unchanged" },
            ProviderData = new Dictionary<string, object> { ["settlement-token"] = settlement },
        };
        var inner = new RecordingReceiver([received]);
        await using var receiver = inner.WithCloudEventDecoding(CloudEventBinding.StructuredOnly);
        var returned = (await receiver.ReceiveAsync(1, CancellationToken.None)).ShouldHaveSingleItem();

        returned.ShouldBeSameAs(received);
        returned.Id.ShouldBe("distinct-broker-envelope");
        returned.Body.Span.SequenceEqual(body).ShouldBeTrue();
        returned.ContentType.ShouldBe(encoded.ContentType);
        returned.Properties["applicationkey"].ShouldBe("unchanged");
        returned.ProviderData["settlement-token"].ShouldBeSameAs(settlement);
        returned.ProviderData.Values.OfType<Exception>().ShouldBeEmpty();
        var actual = returned.ProviderData.Values.OfType<CloudEvent>().ShouldHaveSingleItem();
        actual.Id.ShouldBe(original.Id);
        actual.Type.ShouldBe(original.Type);
        actual.Source.ShouldBe(original.Source);
        actual.Subject.ShouldBe(original.Subject);
        actual.Time.ShouldBe(original.Time);
        actual.DataSchema.ShouldBe(original.DataSchema);
        actual.DataContentType.ShouldBe(original.DataContentType);
        actual.Data.ShouldBe("payload");
        await receiver.AcknowledgeAsync(returned, CancellationToken.None);
        inner.Acknowledged.ShouldHaveSingleItem().ShouldBeSameAs(received);
        return actual;
    }

    private sealed class RecordingSender : ITransportSender
    {
        public List<TransportMessage> Sent { get; } = [];

        public string Destination => "recording";

        public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(message);
            return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
        }

        public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.AddRange(messages);
            return Task.FromResult(new BatchSendResult());
        }

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingReceiver(IReadOnlyList<TransportReceivedMessage> messages) : ITransportReceiver
    {
        public List<TransportReceivedMessage> Acknowledged { get; } = [];

        public string Source => "recording";

        public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TransportReceivedMessage> batch = messages.Take(maxMessages).ToArray();
            return Task.FromResult(batch);
        }

        public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Acknowledged.Add(message);
            return Task.CompletedTask;
        }

        public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This receive fixture supports acknowledgement only.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
