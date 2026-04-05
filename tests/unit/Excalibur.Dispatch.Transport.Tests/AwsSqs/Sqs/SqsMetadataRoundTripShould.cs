// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
[Trait("Category", "MetadataRoundTrip")]
public sealed class SqsMetadataRoundTripShould : IAsyncDisposable
{
    private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

    private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
    private readonly SqsTransportSender _sender;
    private readonly SqsTransportReceiver _receiver;

    public SqsMetadataRoundTripShould()
    {
        _sender = new SqsTransportSender(
            _fakeSqs,
            QueueUrl,
            NullLogger<SqsTransportSender>.Instance);

        _receiver = new SqsTransportReceiver(
            _fakeSqs,
            QueueUrl,
            NullLogger<SqsTransportReceiver>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _receiver.DisposeAsync();
        _fakeSqs.Dispose();
    }

    // =====================================================================
    // SENDER TESTS - Verify TransportMessage metadata maps to SQS MessageAttributes
    // =====================================================================

    [Fact]
    public async Task AllMetadataFields_MappedToMessageAttributes()
    {
        // Arrange
        SendMessageRequest? capturedRequest = null;

        A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
            .Invokes((SendMessageRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(new SendMessageResponse { MessageId = "sqs-rt-1" });

        var message = new TransportMessage
        {
            Id = "msg-round-trip-1",
            Body = Encoding.UTF8.GetBytes("{\"data\":\"test\"}"),
            CorrelationId = "corr-abc-123",
            CausationId = "cause-xyz-789",
            MessageType = "OrderCreated",
            ContentType = "application/json",
            Subject = "orders",
            Properties =
            {
                ["custom-header-1"] = "value-1",
                ["tenant-id"] = "tenant-42",
            },
        };

        // Act
        var result = await _sender.SendAsync(message, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();

        var attrs = capturedRequest!.MessageAttributes;
        attrs.ShouldNotBeNull();

        // Verify standard metadata attributes
        attrs.ShouldContainKey("correlation-id");
        attrs["correlation-id"].StringValue.ShouldBe("corr-abc-123");
        attrs["correlation-id"].DataType.ShouldBe("String");

        attrs.ShouldContainKey("causation-id");
        attrs["causation-id"].StringValue.ShouldBe("cause-xyz-789");
        attrs["causation-id"].DataType.ShouldBe("String");

        attrs.ShouldContainKey("message-type");
        attrs["message-type"].StringValue.ShouldBe("OrderCreated");
        attrs["message-type"].DataType.ShouldBe("String");

        attrs.ShouldContainKey("content-type");
        attrs["content-type"].StringValue.ShouldBe("application/json");
        attrs["content-type"].DataType.ShouldBe("String");

        // Verify custom properties are included
        attrs.ShouldContainKey("custom-header-1");
        attrs["custom-header-1"].StringValue.ShouldBe("value-1");

        attrs.ShouldContainKey("tenant-id");
        attrs["tenant-id"].StringValue.ShouldBe("tenant-42");
    }

    [Fact]
    public async Task CustomProperties_MappedToMessageAttributes_ExceptDispatchPrefix()
    {
        // Arrange
        SendMessageRequest? capturedRequest = null;

        A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
            .Invokes((SendMessageRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(new SendMessageResponse { MessageId = "sqs-filter-1" });

        var message = new TransportMessage
        {
            Id = "msg-filter-1",
            Body = Encoding.UTF8.GetBytes("payload"),
            Properties =
            {
                // dispatch.* prefix properties should be filtered out
                [TransportTelemetryConstants.PropertyKeys.OrderingKey] = "group-1",
                [TransportTelemetryConstants.PropertyKeys.DeduplicationId] = "dedup-1",
                [TransportTelemetryConstants.PropertyKeys.DelaySeconds] = "30",

                // Non-dispatch properties should be included
                ["x-custom-header"] = "keep-me",
                ["tenant-id"] = "t-100",
            },
        };

        // Act
        await _sender.SendAsync(message, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var attrs = capturedRequest!.MessageAttributes;
        attrs.ShouldNotBeNull();

        // dispatch.* properties must NOT appear in message attributes
        attrs.Keys.ShouldNotContain(TransportTelemetryConstants.PropertyKeys.OrderingKey);
        attrs.Keys.ShouldNotContain(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
        attrs.Keys.ShouldNotContain(TransportTelemetryConstants.PropertyKeys.DelaySeconds);

        // Non-dispatch properties must appear
        attrs.ShouldContainKey("x-custom-header");
        attrs["x-custom-header"].StringValue.ShouldBe("keep-me");

        attrs.ShouldContainKey("tenant-id");
        attrs["tenant-id"].StringValue.ShouldBe("t-100");
    }

    [Fact]
    public async Task BatchSend_AllMessages_PreserveUniqueMetadata()
    {
        // Arrange
        SendMessageBatchRequest? capturedBatchRequest = null;

        A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
            .Invokes((SendMessageBatchRequest req, CancellationToken _) => capturedBatchRequest = req)
            .Returns(new SendMessageBatchResponse
            {
                Successful = Enumerable.Range(0, 5)
                    .Select(i => new SendMessageBatchResultEntry { Id = i.ToString(), MessageId = $"sqs-batch-{i}" })
                    .ToList(),
                Failed = [],
            });

        var messages = Enumerable.Range(0, 5).Select(i => new TransportMessage
        {
            Id = $"batch-msg-{i}",
            Body = Encoding.UTF8.GetBytes($"payload-{i}"),
            Properties =
            {
                ["x-unique-trace"] = $"trace-{i}",
                ["tenant-id"] = $"tenant-{i}",
            },
        }).ToList();

        // Act
        var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

        // Assert
        result.SuccessCount.ShouldBe(5);
        capturedBatchRequest.ShouldNotBeNull();
        capturedBatchRequest!.Entries.Count.ShouldBe(5);

        for (var i = 0; i < 5; i++)
        {
            var entry = capturedBatchRequest.Entries[i];
            entry.MessageAttributes.ShouldNotBeNull();
            entry.MessageAttributes.ShouldContainKey("x-unique-trace");
            entry.MessageAttributes["x-unique-trace"].StringValue.ShouldBe($"trace-{i}");
            entry.MessageAttributes.ShouldContainKey("tenant-id");
            entry.MessageAttributes["tenant-id"].StringValue.ShouldBe($"tenant-{i}");
        }
    }

    // =====================================================================
    // RECEIVER TESTS - Verify SQS native format maps back to TransportReceivedMessage
    // =====================================================================

    [Fact]
    public async Task AllMetadataFields_ReconstructedFromMessageAttributes()
    {
        // Arrange
        A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "sqs-recv-1",
                        Body = "{\"data\":\"received\"}",
                        ReceiptHandle = "receipt-rt-1",
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            ["correlation-id"] = new() { DataType = "String", StringValue = "corr-recv-123" },
                            ["causation-id"] = new() { DataType = "String", StringValue = "cause-recv-789" },
                            ["message-type"] = new() { DataType = "String", StringValue = "OrderCreated" },
                            ["content-type"] = new() { DataType = "String", StringValue = "application/json" },
                            ["x-custom-header"] = new() { DataType = "String", StringValue = "custom-value" },
                            ["tenant-id"] = new() { DataType = "String", StringValue = "tenant-42" },
                        },
                        Attributes = new Dictionary<string, string>
                        {
                            ["ApproximateReceiveCount"] = "3",
                            ["SentTimestamp"] = "1700000000000",
                        },
                    },
                ],
            });

        // Act
        var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        // Standard metadata fields reconstructed from message attributes
        received.Id.ShouldBe("sqs-recv-1");
        received.CorrelationId.ShouldBe("corr-recv-123");
        received.MessageType.ShouldBe("OrderCreated");
        received.ContentType.ShouldBe("application/json");

        // CausationId flows through Properties (receiver does not set a top-level CausationId)
        received.Properties.ShouldContainKey("causation-id");
        (received.Properties["causation-id"] as string).ShouldBe("cause-recv-789");

        // Custom attributes are in Properties
        received.Properties.ShouldContainKey("x-custom-header");
        (received.Properties["x-custom-header"] as string).ShouldBe("custom-value");

        received.Properties.ShouldContainKey("tenant-id");
        (received.Properties["tenant-id"] as string).ShouldBe("tenant-42");

        // Delivery count from ApproximateReceiveCount
        received.DeliveryCount.ShouldBe(3);

        // EnqueuedAt from SentTimestamp
        received.EnqueuedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public async Task MissingMetadataAttributes_DoNotCrashReceiver()
    {
        // Arrange - SQS message with NO MessageAttributes at all
        A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "sqs-bare-1",
                        Body = "bare-payload",
                        ReceiptHandle = "receipt-bare-1",
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
                        Attributes = new Dictionary<string, string>(),
                    },
                ],
            });

        // Act
        var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        received.Id.ShouldBe("sqs-bare-1");
        received.ContentType.ShouldBeNull();
        received.MessageType.ShouldBeNull();
        received.CorrelationId.ShouldBeNull();
        received.DeliveryCount.ShouldBe(1); // default when ApproximateReceiveCount absent
        received.Body.ToArray().ShouldBe(Encoding.UTF8.GetBytes("bare-payload"));
    }

    [Fact]
    public async Task SqsSystemAttributes_MappedWithSqsPrefix()
    {
        // Arrange
        A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "sqs-sys-1",
                        Body = "sys-payload",
                        ReceiptHandle = "receipt-sys-1",
                        Attributes = new Dictionary<string, string>
                        {
                            ["ApproximateReceiveCount"] = "7",
                            ["SentTimestamp"] = "1700000000000",
                            ["ApproximateFirstReceiveTimestamp"] = "1700000001000",
                            ["SenderId"] = "AIDAEXAMPLE",
                        },
                    },
                ],
            });

        // Act
        var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        // SQS system attributes should appear with "sqs." prefix in Properties
        received.Properties.ShouldContainKey("sqs.ApproximateReceiveCount");
        (received.Properties["sqs.ApproximateReceiveCount"] as string).ShouldBe("7");

        received.Properties.ShouldContainKey("sqs.SentTimestamp");
        (received.Properties["sqs.SentTimestamp"] as string).ShouldBe("1700000000000");

        received.Properties.ShouldContainKey("sqs.ApproximateFirstReceiveTimestamp");
        (received.Properties["sqs.ApproximateFirstReceiveTimestamp"] as string).ShouldBe("1700000001000");

        received.Properties.ShouldContainKey("sqs.SenderId");
        (received.Properties["sqs.SenderId"] as string).ShouldBe("AIDAEXAMPLE");

        // DeliveryCount derived from ApproximateReceiveCount
        received.DeliveryCount.ShouldBe(7);

        // EnqueuedAt derived from SentTimestamp
        received.EnqueuedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    }

    [Fact]
    public async Task PartialMetadata_PreservesWhatExists()
    {
        // Arrange - Only CorrelationId set, no CausationId, no ContentType, no MessageType
        A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "sqs-partial-1",
                        Body = "partial-payload",
                        ReceiptHandle = "receipt-partial-1",
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            ["correlation-id"] = new() { DataType = "String", StringValue = "corr-only-456" },
                        },
                        Attributes = new Dictionary<string, string>
                        {
                            ["ApproximateReceiveCount"] = "1",
                        },
                    },
                ],
            });

        // Act
        var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        // CorrelationId should be preserved
        received.CorrelationId.ShouldBe("corr-only-456");

        // Fields not set in MessageAttributes should be null
        received.ContentType.ShouldBeNull();
        received.MessageType.ShouldBeNull();

        // CausationId not present in properties either
        received.Properties.ShouldNotContainKey("causation-id");
    }
}
