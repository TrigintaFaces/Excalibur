// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Metadata round-trip tests for Azure Service Bus transport.
/// Verifies that metadata fields on <see cref="TransportMessage"/> are correctly mapped
/// to native <see cref="ServiceBusMessage"/> properties on send, and that native
/// <see cref="ServiceBusReceivedMessage"/> properties are correctly mapped back to
/// <see cref="TransportReceivedMessage"/> on receive.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Category", "MetadataRoundTrip")]
public sealed class ServiceBusMetadataRoundTripShould : IAsyncDisposable
{
    private const string TestDestination = "orders-queue";
    private const string TestSource = "orders-queue";

    // CA2213: FakeItEasy fakes -- disposed via DisposeAsync for analyzer compliance
    private readonly ServiceBusSender _fakeSender;
    private readonly ServiceBusReceiver _fakeReceiver;
    private readonly ServiceBusTransportSender _senderSut;
    private readonly ServiceBusTransportReceiver _receiverSut;

    public ServiceBusMetadataRoundTripShould()
    {
        _fakeSender = A.Fake<ServiceBusSender>();
        _fakeReceiver = A.Fake<ServiceBusReceiver>();

        _senderSut = new ServiceBusTransportSender(
            _fakeSender,
            TestDestination,
            NullLogger<ServiceBusTransportSender>.Instance);

        _receiverSut = new ServiceBusTransportReceiver(
            _fakeReceiver,
            TestSource,
            NullLogger<ServiceBusTransportReceiver>.Instance);
    }

    #region Send: AllMetadataFields_MappedToNativeFormat

    [Fact]
    public async Task AllMetadataFields_MappedToNativeFormat()
    {
        // Arrange
        ServiceBusMessage? capturedMessage = null;
        A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
            .Invokes((ServiceBusMessage msg, CancellationToken _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

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
        capturedMessage.ShouldNotBeNull();

        // Native properties
        capturedMessage.MessageId.ShouldBe("msg-001");
        capturedMessage.ContentType.ShouldBe("application/json");
        capturedMessage.CorrelationId.ShouldBe("corr-123");
        capturedMessage.Subject.ShouldBe("Order.Created");

        // Application properties
        capturedMessage.ApplicationProperties["causation-id"].ShouldBe("cause-456");
        capturedMessage.ApplicationProperties["message-type"].ShouldBe("OrderCreated");
    }

    #endregion

    #region Receive: AllMetadataFields_ReconstructedFromNativeFormat

    [Fact]
    public async Task AllMetadataFields_ReconstructedFromNativeFormat()
    {
        // Arrange
        var appProperties = new Dictionary<string, object>
        {
            ["causation-id"] = "cause-456",
            ["message-type"] = "OrderCreated",
            ["custom-header"] = "custom-value",
        };

        var sbReceived = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes("hello"u8.ToArray()),
            messageId: "msg-001",
            contentType: "application/json",
            correlationId: "corr-123",
            subject: "Order.Created",
            sessionId: "session-1",
            partitionKey: "pk-1",
            lockTokenGuid: Guid.NewGuid(),
            properties: appProperties);

        A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(
                A<int>._, A<TimeSpan?>._, A<CancellationToken>._))
            .Returns(new[] { sbReceived });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        var received = messages[0];

        received.Id.ShouldBe("msg-001");
        received.ContentType.ShouldBe("application/json");
        received.CorrelationId.ShouldBe("corr-123");
        received.Subject.ShouldBe("Order.Created");
        received.MessageType.ShouldBe("OrderCreated");
        received.MessageGroupId.ShouldBe("session-1");
        received.PartitionKey.ShouldBe("pk-1");

        // causation-id is in Properties (ApplicationProperties mapped to Properties)
        received.Properties["causation-id"].ShouldBe("cause-456");
        received.Properties["custom-header"].ShouldBe("custom-value");
    }

    #endregion

    #region Receive: MissingMetadata_DoNotCrashReceiver

    [Fact]
    public async Task MissingMetadata_DoNotCrashReceiver()
    {
        // Arrange - message with no application properties and minimal native properties
        var sbReceived = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes("payload"u8.ToArray()),
            messageId: "msg-minimal",
            lockTokenGuid: Guid.NewGuid());

        A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(
                A<int>._, A<TimeSpan?>._, A<CancellationToken>._))
            .Returns(new[] { sbReceived });

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
        ServiceBusMessage? capturedMessage = null;
        A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
            .Invokes((ServiceBusMessage msg, CancellationToken _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

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
        capturedMessage.ShouldNotBeNull();
        capturedMessage.ApplicationProperties["tenant-id"].ShouldBe("tenant-42");
        capturedMessage.ApplicationProperties["priority"].ShouldBe("high");
        capturedMessage.ApplicationProperties["retry-count"].ShouldBe("3");
    }

    [Fact]
    public async Task CustomProperties_DispatchPrefixed_AreExcluded()
    {
        // Arrange -- dispatch.* keys are transport hints and should not be copied to ApplicationProperties
        ServiceBusMessage? capturedMessage = null;
        A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
            .Invokes((ServiceBusMessage msg, CancellationToken _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var message = new TransportMessage
        {
            Id = "msg-dispatch",
            Body = "data"u8.ToArray(),
            CorrelationId = "corr-dispatch",
            ContentType = "application/json",
        };
        message.Properties["user-header"] = "kept";
        message.Properties["dispatch.ordering-key"] = "session-1";
        message.Properties["dispatch.partition-key"] = "pk-1";

        // Act
        var result = await _senderSut.SendAsync(message, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error?.Message ?? "Send failed");
        capturedMessage.ShouldNotBeNull();
        capturedMessage.ApplicationProperties.ContainsKey("dispatch.ordering-key").ShouldBeFalse(
            "dispatch.* prefixed properties should not be copied to ApplicationProperties");
        capturedMessage.ApplicationProperties.ContainsKey("dispatch.partition-key").ShouldBeFalse();
        capturedMessage.ApplicationProperties["user-header"].ShouldBe("kept");
    }

    #endregion

    #region Send: PartialMetadata_PreservesWhatExists

    [Fact]
    public async Task PartialMetadata_PreservesWhatExists()
    {
        // Arrange - only CorrelationId and Subject set, others null
        ServiceBusMessage? capturedMessage = null;
        A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
            .Invokes((ServiceBusMessage msg, CancellationToken _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

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
        capturedMessage.ShouldNotBeNull();
        capturedMessage.CorrelationId.ShouldBe("corr-only");
        capturedMessage.Subject.ShouldBe("Subject.Only");

        // CausationId not set -> no causation-id in ApplicationProperties
        capturedMessage.ApplicationProperties.ContainsKey("causation-id").ShouldBeFalse();

        // MessageType not set -> no message-type in ApplicationProperties
        capturedMessage.ApplicationProperties.ContainsKey("message-type").ShouldBeFalse();
    }

    #endregion

    // NOTE: ServiceBusMessageBatch is sealed and cannot be faked.
    // Batch metadata preservation is covered via individual SendAsync captures above.

    #region Receive: CausationId_InProperties_NotTopLevel

    [Fact]
    public async Task CausationId_IsAccessible_ViaProperties()
    {
        // Arrange -- CausationId flows through ApplicationProperties -> Properties dictionary
        var appProperties = new Dictionary<string, object>
        {
            ["causation-id"] = "cause-round-trip",
        };

        var sbReceived = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes("data"u8.ToArray()),
            messageId: "msg-causation",
            lockTokenGuid: Guid.NewGuid(),
            properties: appProperties);

        A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(
                A<int>._, A<TimeSpan?>._, A<CancellationToken>._))
            .Returns(new[] { sbReceived });

        // Act
        var messages = await _receiverSut.ReceiveAsync(1, CancellationToken.None);

        // Assert
        messages.Count.ShouldBe(1);
        messages[0].Properties["causation-id"].ShouldBe("cause-round-trip");
    }

    #endregion

    #region Receive: PartialMetadata_PreservesWhatExists

    [Fact]
    public async Task PartialMetadata_OnReceive_PreservesWhatExists()
    {
        // Arrange -- only correlationId and subject are set on the native message
        var sbReceived = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes("data"u8.ToArray()),
            messageId: "msg-partial-recv",
            correlationId: "corr-partial",
            subject: "Subject.Partial",
            lockTokenGuid: Guid.NewGuid());

        A.CallTo(() => _fakeReceiver.ReceiveMessagesAsync(
                A<int>._, A<TimeSpan?>._, A<CancellationToken>._))
            .Returns(new[] { sbReceived });

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

    public async ValueTask DisposeAsync()
    {
        await _senderSut.DisposeAsync().ConfigureAwait(false);
        await _receiverSut.DisposeAsync().ConfigureAwait(false);
        await _fakeSender.DisposeAsync().ConfigureAwait(false);
        await _fakeReceiver.DisposeAsync().ConfigureAwait(false);
    }
}
