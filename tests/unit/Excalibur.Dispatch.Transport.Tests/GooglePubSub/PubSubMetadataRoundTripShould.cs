// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Google;

using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Metadata round-trip tests for Google Cloud Pub/Sub transport.
/// Verifies that metadata fields on <see cref="TransportMessage"/> are correctly mapped
/// to <see cref="PubsubMessage"/> attributes on send, and that <see cref="PubsubMessage"/>
/// attributes are correctly mapped back to <see cref="TransportReceivedMessage"/> on receive.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Category", "MetadataRoundTrip")]
public sealed class PubSubMetadataRoundTripShould : IAsyncDisposable
{
    private const string TestTopic = "projects/test-project/topics/orders";
    private const string TestSubscription = "projects/test-project/subscriptions/orders-sub";

    private readonly PublisherServiceApiClient _fakePublisher;
    private readonly SubscriberServiceApiClient _fakeSubscriber;
    private readonly PubSubTransportSender _senderSut;
    private readonly PubSubTransportReceiver _receiverSut;

    public PubSubMetadataRoundTripShould()
    {
        _fakePublisher = A.Fake<PublisherServiceApiClient>();
        _fakeSubscriber = A.Fake<SubscriberServiceApiClient>();

        _senderSut = new PubSubTransportSender(
            _fakePublisher,
            TestTopic,
            NullLogger<PubSubTransportSender>.Instance);

        _receiverSut = new PubSubTransportReceiver(
            _fakeSubscriber,
            TestSubscription,
            NullLogger<PubSubTransportReceiver>.Instance);
    }

    #region Send: AllMetadataFields_MappedToNativeFormat

    [Fact]
    public async Task AllMetadataFields_MappedToNativeFormat()
    {
        // Arrange
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-id-1" } });

        var message = new TransportMessage
        {
            Id = "msg-001",
            Body = "hello"u8.ToArray(),
            ContentType = "application/json",
            MessageType = "OrderCreated",
            CorrelationId = "corr-123",
            CausationId = "cause-456",
            Subject = "Order.Created",
        };

        // Act
        var result = await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Messages.Count.ShouldBe(1);

        var pubsubMessage = capturedRequest.Messages[0];

        // All metadata mapped to attributes
        pubsubMessage.Attributes["message-id"].ShouldBe("msg-001");
        pubsubMessage.Attributes["content-type"].ShouldBe("application/json");
        pubsubMessage.Attributes["message-type"].ShouldBe("OrderCreated");
        pubsubMessage.Attributes["correlation-id"].ShouldBe("corr-123");
        pubsubMessage.Attributes["causation-id"].ShouldBe("cause-456");
        pubsubMessage.Attributes["subject"].ShouldBe("Order.Created");

        // Body
        pubsubMessage.Data.ToByteArray().ShouldBe("hello"u8.ToArray());
    }

    #endregion

    #region Receive: AllMetadataFields_ReconstructedFromNativeFormat

    [Fact]
    public async Task AllMetadataFields_ReconstructedFromNativeFormat()
    {
        // Arrange
        var pubsubMessage = new PubsubMessage
        {
            MessageId = "server-msg-001",
            Data = ByteString.CopyFromUtf8("hello"),
            OrderingKey = "order-group-1",
            PublishTime = global::Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Attributes =
            {
                ["message-id"] = "msg-001",
                ["content-type"] = "application/json",
                ["message-type"] = "OrderCreated",
                ["correlation-id"] = "corr-123",
                ["causation-id"] = "cause-456",
                ["subject"] = "Order.Created",
                ["ordering-key"] = "order-group-1",
                ["custom-header"] = "custom-value",
            },
        };

        var receivedMessage = new ReceivedMessage
        {
            AckId = "ack-001",
            Message = pubsubMessage,
            DeliveryAttempt = 2,
        };

        A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CallSettings>._))
            .Returns(new PullResponse { ReceivedMessages = { receivedMessage } });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        // Server-assigned MessageId takes precedence over attribute "message-id"
        received.Id.ShouldBe("server-msg-001");
        received.ContentType.ShouldBe("application/json");
        received.MessageType.ShouldBe("OrderCreated");
        received.CorrelationId.ShouldBe("corr-123");
        received.Subject.ShouldBe("Order.Created");
        received.DeliveryCount.ShouldBe(2);
        received.Source.ShouldBe(TestSubscription);

        // OrderingKey maps to both MessageGroupId and PartitionKey
        received.MessageGroupId.ShouldBe("order-group-1");
        received.PartitionKey.ShouldBe("order-group-1");

        // Custom properties survive
        received.Properties["custom-header"].ShouldBe("custom-value");

        // causation-id is in Properties
        received.Properties["causation-id"].ShouldBe("cause-456");

        // ProviderData contains ack_id
        received.ProviderData["pubsub.ack_id"].ShouldBe("ack-001");
    }

    #endregion

    #region Receive: MissingMetadata_DoNotCrashReceiver

    [Fact]
    public async Task MissingMetadata_DoNotCrashReceiver()
    {
        // Arrange -- message with no attributes and minimal fields
        var pubsubMessage = new PubsubMessage
        {
            MessageId = "msg-minimal",
            Data = ByteString.CopyFromUtf8("payload"),
            PublishTime = global::Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };

        var receivedMessage = new ReceivedMessage
        {
            AckId = "ack-minimal",
            Message = pubsubMessage,
        };

        A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CallSettings>._))
            .Returns(new PullResponse { ReceivedMessages = { receivedMessage } });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        received.Id.ShouldBe("msg-minimal");
        received.ContentType.ShouldBeNull();
        received.CorrelationId.ShouldBeNull();
        received.Subject.ShouldBeNull();
        received.MessageType.ShouldBeNull();
        received.MessageGroupId.ShouldBeNull();
        received.PartitionKey.ShouldBeNull();
    }

    #endregion

    #region Send: CustomProperties_SurviveRoundTrip

    [Fact]
    public async Task CustomProperties_SurviveRoundTrip()
    {
        // Arrange
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-1" } });

        var message = new TransportMessage
        {
            Id = "msg-custom",
            Body = "data"u8.ToArray(),
            Properties =
            {
                ["tenant-id"] = "tenant-42",
                ["priority"] = "high",
                ["retry-count"] = "3",
            },
        };

        // Act
        await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var pubsubMessage = capturedRequest.Messages[0];
        pubsubMessage.Attributes["tenant-id"].ShouldBe("tenant-42");
        pubsubMessage.Attributes["priority"].ShouldBe("high");
        pubsubMessage.Attributes["retry-count"].ShouldBe("3");
    }

    [Fact]
    public async Task CustomProperties_DispatchPrefixed_AreExcluded()
    {
        // Arrange -- dispatch.* keys are transport hints and should not be copied to attributes
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-1" } });

        var message = new TransportMessage
        {
            Id = "msg-dispatch",
            Body = "data"u8.ToArray(),
            Properties =
            {
                ["dispatch.ordering.key"] = "session-1",
                ["dispatch.partition.key"] = "pk-1",
                ["user-header"] = "kept",
            },
        };

        // Act
        await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var pubsubMessage = capturedRequest.Messages[0];
        pubsubMessage.Attributes.ContainsKey("dispatch.ordering.key").ShouldBeFalse();
        pubsubMessage.Attributes.ContainsKey("dispatch.partition.key").ShouldBeFalse();
        pubsubMessage.Attributes["user-header"].ShouldBe("kept");
    }

    #endregion

    #region Send: PartialMetadata_PreservesWhatExists

    [Fact]
    public async Task PartialMetadata_PreservesWhatExists()
    {
        // Arrange - only CorrelationId and Subject set, others null
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-1" } });

        var message = new TransportMessage
        {
            Id = "msg-partial",
            Body = "data"u8.ToArray(),
            CorrelationId = "corr-only",
            Subject = "Subject.Only",
            // CausationId, MessageType, ContentType all null
        };

        // Act
        await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var pubsubMessage = capturedRequest.Messages[0];

        pubsubMessage.Attributes["correlation-id"].ShouldBe("corr-only");
        pubsubMessage.Attributes["subject"].ShouldBe("Subject.Only");
        pubsubMessage.Attributes["message-id"].ShouldBe("msg-partial");

        // Null fields should not be in attributes
        pubsubMessage.Attributes.ContainsKey("causation-id").ShouldBeFalse();
        pubsubMessage.Attributes.ContainsKey("message-type").ShouldBeFalse();
        pubsubMessage.Attributes.ContainsKey("content-type").ShouldBeFalse();
    }

    #endregion

    #region Send: OrderingKey_MapsToNativeAndAttribute

    [Fact]
    public async Task OrderingKey_MapsToNativeOrderingKeyAndAttribute()
    {
        // Arrange
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-1" } });

        var message = new TransportMessage
        {
            Id = "msg-ordering",
            Body = "data"u8.ToArray(),
            Properties =
            {
                [TransportTelemetryConstants.PropertyKeys.OrderingKey] = "order-group-1",
            },
        };

        // Act
        await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        var pubsubMessage = capturedRequest.Messages[0];

        // Native ordering key
        pubsubMessage.OrderingKey.ShouldBe("order-group-1");

        // Also in attributes for round-trip
        pubsubMessage.Attributes["ordering-key"].ShouldBe("order-group-1");
    }

    #endregion

    #region Send: BatchSend_PreservesPerMessageMetadata

    [Fact]
    public async Task BatchSend_PreservesPerMessageMetadata()
    {
        // Arrange
        PublishRequest? capturedRequest = null;
        A.CallTo(() => _fakePublisher.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
            .Invokes((PublishRequest req, CallSettings _) => capturedRequest = req)
            .Returns(new PublishResponse { MessageIds = { "server-1", "server-2" } });

        var messages = new List<TransportMessage>
        {
            new()
            {
                Id = "batch-1",
                Body = "one"u8.ToArray(),
                CorrelationId = "corr-1",
                CausationId = "cause-1",
                MessageType = "TypeA",
                Subject = "Subject.A",
                ContentType = "application/json",
            },
            new()
            {
                Id = "batch-2",
                Body = "two"u8.ToArray(),
                CorrelationId = "corr-2",
                CausationId = "cause-2",
                MessageType = "TypeB",
                Subject = "Subject.B",
                ContentType = "text/plain",
            },
        };

        // Act
        var result = await _senderSut.SendBatchAsync(messages, CancellationToken.None);

        // Assert
        result.TotalMessages.ShouldBe(2);
        result.SuccessCount.ShouldBe(2);
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Messages.Count.ShouldBe(2);

        // Verify first message
        var msg1 = capturedRequest.Messages[0];
        msg1.Attributes["message-id"].ShouldBe("batch-1");
        msg1.Attributes["correlation-id"].ShouldBe("corr-1");
        msg1.Attributes["causation-id"].ShouldBe("cause-1");
        msg1.Attributes["message-type"].ShouldBe("TypeA");
        msg1.Attributes["subject"].ShouldBe("Subject.A");
        msg1.Attributes["content-type"].ShouldBe("application/json");

        // Verify second message
        var msg2 = capturedRequest.Messages[1];
        msg2.Attributes["message-id"].ShouldBe("batch-2");
        msg2.Attributes["correlation-id"].ShouldBe("corr-2");
        msg2.Attributes["causation-id"].ShouldBe("cause-2");
        msg2.Attributes["message-type"].ShouldBe("TypeB");
        msg2.Attributes["subject"].ShouldBe("Subject.B");
        msg2.Attributes["content-type"].ShouldBe("text/plain");
    }

    #endregion

    #region Receive: PartialMetadata_OnReceive_PreservesWhatExists

    [Fact]
    public async Task PartialMetadata_OnReceive_PreservesWhatExists()
    {
        // Arrange -- only correlation-id and subject are in attributes
        var pubsubMessage = new PubsubMessage
        {
            MessageId = "msg-partial-recv",
            Data = ByteString.CopyFromUtf8("data"),
            PublishTime = global::Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Attributes =
            {
                ["correlation-id"] = "corr-partial",
                ["subject"] = "Subject.Partial",
            },
        };

        var receivedMessage = new ReceivedMessage
        {
            AckId = "ack-partial",
            Message = pubsubMessage,
        };

        A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CallSettings>._))
            .Returns(new PullResponse { ReceivedMessages = { receivedMessage } });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        var received = messages[0];
        received.CorrelationId.ShouldBe("corr-partial");
        received.Subject.ShouldBe("Subject.Partial");
        received.ContentType.ShouldBeNull();
        received.MessageType.ShouldBeNull();
    }

    #endregion

    #region Receive: EmptyOrderingKey_IsNull

    [Fact]
    public async Task EmptyOrderingKey_MapsToNullMessageGroupId()
    {
        // Arrange -- OrderingKey is empty string (Pub/Sub default)
        var pubsubMessage = new PubsubMessage
        {
            MessageId = "msg-no-ordering",
            Data = ByteString.CopyFromUtf8("data"),
            OrderingKey = "",
            PublishTime = global::Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };

        var receivedMessage = new ReceivedMessage
        {
            AckId = "ack-no-ordering",
            Message = pubsubMessage,
        };

        A.CallTo(() => _fakeSubscriber.PullAsync(A<PullRequest>._, A<CallSettings>._))
            .Returns(new PullResponse { ReceivedMessages = { receivedMessage } });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        messages[0].MessageGroupId.ShouldBeNull();
        messages[0].PartitionKey.ShouldBeNull();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _senderSut.DisposeAsync().ConfigureAwait(false);
        await _receiverSut.DisposeAsync().ConfigureAwait(false);
    }
}
