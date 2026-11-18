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
public sealed class SharedCloudEventBinaryBoundariesShould
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(383, false)]
    [InlineData(384, false)]
    [InlineData(385, false)]
    [InlineData(65536, false)]
    [InlineData(385, true)]
    [InlineData(65536, true)]
    public async Task PreserveExactBinaryBytesEscapingAndCallerMemoryAcrossScratchBoundaries(int length, bool memorySlice)
    {
        var backing = new byte[length + 10];
        Array.Fill(backing, (byte)0xA5);
        for (var index = 0; index < length; index++)
        {
            backing[index + 5] = (byte)((index * 73 + 251) % 256);
        }
        var before = backing.ToArray();
        var expected = backing.AsSpan(5, length).ToArray();
        var directBytes = expected.ToArray();
        object payload = memorySlice ? (object)new ReadOnlyMemory<byte>(backing, 5, length) : directBytes;
        if (memorySlice)
        {
            payload.ShouldBeOfType<ReadOnlyMemory<byte>>();
        }
        else
        {
            payload.ShouldBeOfType<byte[]>();
        }
        var actualEvent = Event(payload);
        var oracleEvent = Event(expected); // Pinned SDK supports byte[] binary; the public ROM path selects exactly this subset.
        var oracle = Formatter().EncodeStructuredModeMessage(oracleEvent, out _);
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();

        await sender.SendAsync(TransportMessage.FromString("original").WithCloudEvent(actualEvent), CancellationToken.None);

        var sent = inner.Sent.ShouldHaveSingleItem();
        sent.Body.ToArray().ShouldBe(oracle.ToArray(), "literal escaping is part of the pinned upstream byte criterion");
        using var document = JsonDocument.Parse(sent.Body);
        document.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
        document.RootElement.GetProperty("data_base64").GetBytesFromBase64().ShouldBe(expected);
        if (length > 0)
        {
            Encoding.UTF8.GetString(sent.Body.Span).ShouldContain("\\u002B");
        }
        backing.ShouldBe(before, "scratch clearing must never clear a borrowed caller memory slice");
        directBytes.ShouldBe(expected, "scratch clearing must never clear an explicit caller byte array");
    }

    [Fact]
    public async Task EmitOnlyTheCurrentWrittenPrefixAfterALargeBinaryMessage()
    {
        var large = Enumerable.Repeat((byte)251, 65536).ToArray();
        var small = new byte[] { 251 };
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();
        var first = Event(large);
        var second = Event(small);

        await sender.SendAsync(TransportMessage.FromString("first").WithCloudEvent(first), CancellationToken.None);
        await sender.SendAsync(TransportMessage.FromString("second").WithCloudEvent(second), CancellationToken.None);

        inner.Sent.Count.ShouldBe(2);
        inner.Sent[0].Body.ToArray().ShouldBe(Formatter().EncodeStructuredModeMessage(Event(large), out _).ToArray());
        inner.Sent[1].Body.ToArray().ShouldBe(Formatter().EncodeStructuredModeMessage(Event(small), out _).ToArray());
        large.ShouldAllBe(value => value == 251);
        small.ShouldBe(new byte[] { 251 });
    }

    [Fact]
    public async Task PreserveCallerBytesAndRetryTheSameMessageAfterALaterValidatorRefuses()
    {
        var payload = Enumerable.Repeat((byte)251, 65536).ToArray();
        var accept = true;
        var binary = CloudEventAttribute.CreateExtension("binaryvalue", CloudEventAttributeType.Binary);
        var gate = CloudEventAttribute.CreateExtension("gate", CloudEventAttributeType.Boolean, _ =>
        {
            if (!accept)
            {
                throw new InvalidOperationException("Refuse after the preceding binary attribute.");
            }
        });
        var cloudEvent = Event("payload");
        cloudEvent[binary] = payload;
        cloudEvent[gate] = false;
        var message = TransportMessage.FromString("original").WithCloudEvent(cloudEvent);
        message.ContentType = "text/plain";
        await using var inner = new RecordingSender();
        await using var sender = inner.WithCloudEventEncoding();
        accept = false;

        await Should.ThrowAsync<ArgumentException>(() => sender.SendAsync(message, CancellationToken.None));

        inner.Sent.ShouldBeEmpty();
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("original");
        message.ContentType.ShouldBe("text/plain");
        message.Properties.Values.ShouldContain(value => ReferenceEquals(value, cloudEvent));
        payload.ShouldAllBe(value => value == 251);
        accept = true;
        await sender.SendAsync(message, CancellationToken.None);
        inner.Sent.ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.Body.ToArray().ShouldBe(Formatter().EncodeStructuredModeMessage(cloudEvent, out _).ToArray());
        payload.ShouldAllBe(value => value == 251);
    }

    private static CloudEvent Event(object data) => new(CloudEventsSpecVersion.V1_0)
    {
        Id = "binary-boundary",
        Type = "conformance.binary",
        Source = new Uri("https://example.test/binary"),
        DataContentType = "application/json", // Actual binary remains binary despite this declaration.
        Data = data,
    };

    private static JsonEventFormatter Formatter() => new(UpstreamEnvelopeOracleContext.Default.Options, default);

    private sealed class RecordingSender : ITransportSender
    {
        public List<TransportMessage> Sent { get; } = [];

        public string Destination => "binary-boundaries";

        public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(message);
            return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
        }

        public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Individual public sends exercise the binary boundary.");

        public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
