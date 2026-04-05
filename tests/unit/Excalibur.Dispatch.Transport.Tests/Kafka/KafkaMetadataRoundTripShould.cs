// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- FakeItEasy .Returns() stores ValueTask
#pragma warning disable CS8620 // Nullability mismatch in FakeItEasy ReturnsLazily for nullable ConsumeResult

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Verifies that metadata fields survive the full Kafka send/receive round trip:
/// TransportMessage -> Kafka headers -> ConsumeResult -> TransportReceivedMessage.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaMetadataRoundTripShould : IAsyncDisposable
{
    private const string TestTopic = "round-trip-topic";

    private readonly IProducer<string, byte[]> _fakeProducer;
    private readonly IConsumer<string, byte[]> _fakeConsumer;
    private readonly KafkaTransportSender _sender;
    private readonly KafkaTransportReceiver _receiver;

    /// <summary>
    /// Captures the Kafka message passed to ProduceAsync so tests can inspect headers and key.
    /// </summary>
    private Message<string, byte[]>? _capturedMessage;

    public KafkaMetadataRoundTripShould()
    {
        _fakeProducer = A.Fake<IProducer<string, byte[]>>();
        _fakeConsumer = A.Fake<IConsumer<string, byte[]>>();

        // Capture the Kafka message sent via ProduceAsync.
        A.CallTo(() => _fakeProducer.ProduceAsync(
                A<string>._,
                A<Message<string, byte[]>>._,
                A<CancellationToken>._))
            .Invokes((string _, Message<string, byte[]> msg, CancellationToken _) => _capturedMessage = msg)
            .ReturnsLazily(() => new DeliveryResult<string, byte[]>
            {
                Partition = new Partition(0),
                Offset = new Offset(42),
                Topic = TestTopic,
                Message = _capturedMessage!,
            });

        _sender = new KafkaTransportSender(
            _fakeProducer,
            TestTopic,
            NullLogger<KafkaTransportSender>.Instance);

        _receiver = new KafkaTransportReceiver(
            _fakeConsumer,
            TestTopic,
            NullLogger<KafkaTransportReceiver>.Instance);
    }

    [Fact]
    public async Task AllMetadataFields_MappedToKafkaHeaders()
    {
        // Arrange
        var message = CreateFullyPopulatedMessage();

        // Act
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert
        _capturedMessage.ShouldNotBeNull();
        var headers = _capturedMessage.Headers;

        GetHeaderValue(headers, "correlation-id").ShouldBe("corr-123");
        GetHeaderValue(headers, "causation-id").ShouldBe("cause-456");
        GetHeaderValue(headers, "message-type").ShouldBe("OrderPlaced");
        GetHeaderValue(headers, "content-type").ShouldBe("application/json");
        GetHeaderValue(headers, "subject").ShouldBe("orders.created");
        GetHeaderValue(headers, "message-id").ShouldBe(message.Id);
    }

    [Fact]
    public async Task AllMetadataFields_ReconstructedFromKafkaHeaders()
    {
        // Arrange -- build a ConsumeResult that simulates what Kafka would deliver
        var messageId = "recv-msg-001";
        var headers = new Headers
        {
            { "correlation-id", Encoding.UTF8.GetBytes("corr-999") },
            { "causation-id", Encoding.UTF8.GetBytes("cause-888") },
            { "message-type", Encoding.UTF8.GetBytes("OrderShipped") },
            { "content-type", Encoding.UTF8.GetBytes("application/json") },
            { "subject", Encoding.UTF8.GetBytes("orders.shipped") },
            { "message-id", Encoding.UTF8.GetBytes(messageId) },
        };

        var consumeResult = CreateConsumeResult("key-1", "hello"u8.ToArray(), headers);
        SetupConsumerToReturn(consumeResult);

        // Act
        var received = await _receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

        // Assert
        received.Count.ShouldBe(1);
        var msg = received[0];

        msg.Id.ShouldBe(messageId);
        msg.CorrelationId.ShouldBe("corr-999");
        msg.MessageType.ShouldBe("OrderShipped");
        msg.ContentType.ShouldBe("application/json");

        // causation-id and subject are in Properties (receiver does not have named properties for them)
        msg.Properties.ShouldContainKey("causation-id");
        ((string)msg.Properties["causation-id"]).ShouldBe("cause-888");
        msg.Properties.ShouldContainKey("subject");
        ((string)msg.Properties["subject"]).ShouldBe("orders.shipped");
    }

    [Fact]
    public async Task MissingHeaders_DoNotCrashReceiver()
    {
        // Arrange -- ConsumeResult with NO headers at all
        var consumeResult = CreateConsumeResult("key-no-headers", "body"u8.ToArray(), headers: null);
        SetupConsumerToReturn(consumeResult);

        // Act
        var received = await _receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

        // Assert
        received.Count.ShouldBe(1);
        var msg = received[0];

        // When no message-id header, receiver falls back to Key or receipt handle
        msg.Id.ShouldBe("key-no-headers");
        msg.CorrelationId.ShouldBeNull();
        msg.ContentType.ShouldBeNull();
        msg.MessageType.ShouldBeNull();
    }

    [Fact]
    public async Task CustomProperties_SurviveRoundTrip()
    {
        // Arrange -- send with custom (non-dispatch.*) properties
        var message = new TransportMessage
        {
            Body = "payload"u8.ToArray(),
            Properties =
            {
                ["x-custom-tenant"] = "tenant-42",
                ["x-trace-flags"] = "01",
                // dispatch.* keys should be excluded from headers
                [TransportTelemetryConstants.PropertyKeys.OrderingKey] = "order-key",
            },
        };

        // Act -- send
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert -- custom properties become headers, dispatch.* do not
        _capturedMessage.ShouldNotBeNull();
        var headers = _capturedMessage.Headers;

        GetHeaderValue(headers, "x-custom-tenant").ShouldBe("tenant-42");
        GetHeaderValue(headers, "x-trace-flags").ShouldBe("01");
        GetHeaderValue(headers, TransportTelemetryConstants.PropertyKeys.OrderingKey).ShouldBeNull();

        // Now simulate receive with those same headers
        var receiveHeaders = new Headers
        {
            { "x-custom-tenant", Encoding.UTF8.GetBytes("tenant-42") },
            { "x-trace-flags", Encoding.UTF8.GetBytes("01") },
            { "message-id", Encoding.UTF8.GetBytes(message.Id) },
        };

        var consumeResult = CreateConsumeResult(message.Id, "payload"u8.ToArray(), receiveHeaders);
        SetupConsumerToReturn(consumeResult);

        var received = await _receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);
        received.Count.ShouldBe(1);

        ((string)received[0].Properties["x-custom-tenant"]).ShouldBe("tenant-42");
        ((string)received[0].Properties["x-trace-flags"]).ShouldBe("01");
    }

    [Fact]
    public async Task UTF8Encoding_PreservesUnicodeInHeaders()
    {
        // Arrange
        var unicodeSubject = "pedidos.cre\u00e1dos \u2014 \u4e16\u754c";
        var unicodeCorrelation = "korr-\u00e9l\u00e1ci\u00f3-\u00fc\u00f1\u00ee";

        var message = new TransportMessage
        {
            Body = "test"u8.ToArray(),
            Subject = unicodeSubject,
            CorrelationId = unicodeCorrelation,
        };

        // Act -- send
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert -- verify UTF-8 encoding preserved in headers
        _capturedMessage.ShouldNotBeNull();
        GetHeaderValue(_capturedMessage.Headers, "subject").ShouldBe(unicodeSubject);
        GetHeaderValue(_capturedMessage.Headers, "correlation-id").ShouldBe(unicodeCorrelation);

        // Simulate receive with same UTF-8 bytes
        var receiveHeaders = new Headers
        {
            { "subject", Encoding.UTF8.GetBytes(unicodeSubject) },
            { "correlation-id", Encoding.UTF8.GetBytes(unicodeCorrelation) },
            { "message-id", Encoding.UTF8.GetBytes(message.Id) },
        };

        var consumeResult = CreateConsumeResult(message.Id, "test"u8.ToArray(), receiveHeaders);
        SetupConsumerToReturn(consumeResult);

        var received = await _receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);
        received.Count.ShouldBe(1);

        received[0].CorrelationId.ShouldBe(unicodeCorrelation);
        ((string)received[0].Properties["subject"]).ShouldBe(unicodeSubject);
    }

    [Fact]
    public async Task OrderingKey_MappedToMessageKey()
    {
        // Arrange -- dispatch.ordering.key takes priority over partition key
        var message = new TransportMessage
        {
            Body = "keyed"u8.ToArray(),
            Properties =
            {
                [TransportTelemetryConstants.PropertyKeys.OrderingKey] = "order-group-7",
                [TransportTelemetryConstants.PropertyKeys.PartitionKey] = "fallback-pk",
            },
        };

        // Act
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert -- ordering key wins
        _capturedMessage.ShouldNotBeNull();
        _capturedMessage.Key.ShouldBe("order-group-7");
    }

    [Fact]
    public async Task PartitionKey_UsedAsFallbackMessageKey()
    {
        // Arrange -- only partition key, no ordering key
        var message = new TransportMessage
        {
            Body = "pk-only"u8.ToArray(),
            Properties =
            {
                [TransportTelemetryConstants.PropertyKeys.PartitionKey] = "partition-abc",
            },
        };

        // Act
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert
        _capturedMessage.ShouldNotBeNull();
        _capturedMessage.Key.ShouldBe("partition-abc");
    }

    [Fact]
    public async Task MessageId_UsedAsDefaultKey_WhenNoOrderingOrPartitionKey()
    {
        // Arrange -- no ordering or partition key
        var message = new TransportMessage
        {
            Body = "default-key"u8.ToArray(),
        };

        // Act
        await _sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

        // Assert -- falls back to message.Id
        _capturedMessage.ShouldNotBeNull();
        _capturedMessage.Key.ShouldBe(message.Id);
    }

    [Fact]
    public async Task BatchSend_PreservesPerMessageMetadata()
    {
        // Arrange -- three messages with unique metadata
        var capturedMessages = new List<Message<string, byte[]>>();

        A.CallTo(() => _fakeProducer.ProduceAsync(
                A<string>._,
                A<Message<string, byte[]>>._,
                A<CancellationToken>._))
            .Invokes((string _, Message<string, byte[]> msg, CancellationToken _) => capturedMessages.Add(msg))
            .ReturnsLazily(() => new DeliveryResult<string, byte[]>
            {
                Partition = new Partition(0),
                Offset = new Offset(capturedMessages.Count),
                Topic = TestTopic,
                Message = capturedMessages[^1],
            });

        var messages = new List<TransportMessage>
        {
            new()
            {
                Body = "msg1"u8.ToArray(),
                CorrelationId = "corr-A",
                MessageType = "TypeA",
            },
            new()
            {
                Body = "msg2"u8.ToArray(),
                CorrelationId = "corr-B",
                MessageType = "TypeB",
                Subject = "batch-subject",
            },
            new()
            {
                Body = "msg3"u8.ToArray(),
                CausationId = "cause-C",
                ContentType = "text/plain",
            },
        };

        // Act
        var batchResult = await _sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

        // Assert
        batchResult.TotalMessages.ShouldBe(3);
        batchResult.SuccessCount.ShouldBe(3);
        capturedMessages.Count.ShouldBe(3);

        // Message 1: corr-A, TypeA
        GetHeaderValue(capturedMessages[0].Headers, "correlation-id").ShouldBe("corr-A");
        GetHeaderValue(capturedMessages[0].Headers, "message-type").ShouldBe("TypeA");

        // Message 2: corr-B, TypeB, subject
        GetHeaderValue(capturedMessages[1].Headers, "correlation-id").ShouldBe("corr-B");
        GetHeaderValue(capturedMessages[1].Headers, "message-type").ShouldBe("TypeB");
        GetHeaderValue(capturedMessages[1].Headers, "subject").ShouldBe("batch-subject");

        // Message 3: cause-C, text/plain, no correlation
        GetHeaderValue(capturedMessages[2].Headers, "causation-id").ShouldBe("cause-C");
        GetHeaderValue(capturedMessages[2].Headers, "content-type").ShouldBe("text/plain");
        GetHeaderValue(capturedMessages[2].Headers, "correlation-id").ShouldBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync().ConfigureAwait(false);
        await _receiver.DisposeAsync().ConfigureAwait(false);
        _fakeProducer.Dispose();
        _fakeConsumer.Dispose();
    }

    // -- Helpers --

    private static TransportMessage CreateFullyPopulatedMessage() => new()
    {
        Body = "test-body"u8.ToArray(),
        ContentType = "application/json",
        MessageType = "OrderPlaced",
        CorrelationId = "corr-123",
        CausationId = "cause-456",
        Subject = "orders.created",
    };

    private static Confluent.Kafka.ConsumeResult<string, byte[]> CreateConsumeResult(
        string key,
        byte[] value,
        Headers? headers)
    {
        return new Confluent.Kafka.ConsumeResult<string, byte[]>
        {
            Topic = TestTopic,
            Partition = new Partition(0),
            Offset = new Offset(1),
            Message = new Message<string, byte[]>
            {
                Key = key,
                Value = value,
                Headers = headers,
                Timestamp = new Timestamp(DateTimeOffset.UtcNow),
            },
        };
    }

    private void SetupConsumerToReturn(Confluent.Kafka.ConsumeResult<string, byte[]> result)
    {
        var callCount = 0;
        A.CallTo(() => _fakeConsumer.Consume(A<TimeSpan>._))
            .ReturnsLazily(() => callCount++ == 0 ? result : null);
    }

    private static string? GetHeaderValue(Headers headers, string key)
    {
        if (headers.TryGetLastBytes(key, out var bytes))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }
}
