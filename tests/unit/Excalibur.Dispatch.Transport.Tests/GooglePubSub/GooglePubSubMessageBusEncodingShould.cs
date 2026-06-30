// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using FakeItEasy;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Encoding tests for <see cref="GooglePubSubMessageBus"/> (z1p330). Verifies the publish path writes
/// the serializer's RAW bytes to the binary <see cref="PubsubMessage.Data"/> field (no base64 inflation)
/// and that those bytes round-trip byte-identically through the real receive path, and that the publish
/// path honors its <see cref="CancellationToken"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Category", "Encoding")]
public sealed class GooglePubSubMessageBusEncodingShould
{
    private const string TestSubscription = "projects/test-project/subscriptions/orders-sub";

    // A payload whose base64 encoding differs from the raw bytes — so a base64 publish would NOT
    // round-trip byte-identically through the receive path (which reads raw PubsubMessage.Data).
    private static readonly byte[] s_payload = [0x00, 0x01, 0x02, 0xFE, 0xFF, 0x10, 0x42, 0x7A];

    [Fact]
    public async Task WriteRawSerializedBytesToData_ThatRoundTripThroughReceivePath()
    {
        // Arrange
        var serializer = A.Fake<IPayloadSerializer>();
        _ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(s_payload);

        PubsubMessage? published = null;
        var publisher = A.Fake<ITopicPublisherClientSeam>();
        _ = A.CallTo(() => publisher.PublishAsync(A<PubsubMessage>._))
            .Invokes((PubsubMessage m) => published = m)
            .Returns(Task.FromResult("server-id-1"));

        var bus = new GooglePubSubMessageBus(
            publisher,
            serializer,
            new GooglePubSubOptions(),
            NullLogger<GooglePubSubMessageBus>.Instance);

        var evt = A.Fake<IDispatchEvent>();
        var context = new MessageContext(evt, new ServiceCollection().BuildServiceProvider());

        // Act — publish
        await bus.PublishAsync(evt, context, CancellationToken.None);

        // Assert — Data carries the RAW serialized bytes, NOT base64 (RED on Convert.ToBase64String).
        published.ShouldNotBeNull();
        published.Data.ToByteArray().ShouldBe(s_payload);

        // Assert — those exact bytes round-trip byte-identically through the REAL receive path,
        // which reads raw PubsubMessage.Data (proves producer↔consumer encoding symmetry).
        var fakeSubscriber = A.Fake<ISubscriberApiClientSeam>();
        _ = A.CallTo(() => fakeSubscriber.PullAsync(A<PullRequest>._, A<CancellationToken>._))
            .Returns(new PullResponse
            {
                ReceivedMessages =
                {
                    new ReceivedMessage
                    {
                        AckId = "ack-1",
                        Message = new PubsubMessage { MessageId = "m1", Data = published.Data },
                    },
                },
            });

        await using var receiver = new PubSubTransportReceiver(
            fakeSubscriber,
            TestSubscription,
            NullLogger<PubSubTransportReceiver>.Instance);

        var received = await receiver.ReceiveAsync(1, CancellationToken.None);

        received.Count.ShouldBe(1);
        received[0].Body.ToArray().ShouldBe(s_payload);
    }

    [Fact]
    public async Task HonorCancellationToken_AndNotPublish_WhenAlreadyCancelled()
    {
        // Arrange
        var serializer = A.Fake<IPayloadSerializer>();
        _ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(s_payload);

        var publisher = A.Fake<ITopicPublisherClientSeam>();
        _ = A.CallTo(() => publisher.PublishAsync(A<PubsubMessage>._)).Returns(Task.FromResult("id"));

        var bus = new GooglePubSubMessageBus(
            publisher,
            serializer,
            new GooglePubSubOptions(),
            NullLogger<GooglePubSubMessageBus>.Instance);

        var evt = A.Fake<IDispatchEvent>();
        var context = new MessageContext(evt, new ServiceCollection().BuildServiceProvider());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert — cancellation is honored before publishing (RED on the CT-ignoring impl).
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => bus.PublishAsync(evt, context, cts.Token));

        A.CallTo(() => publisher.PublishAsync(A<PubsubMessage>._)).MustNotHaveHappened();
    }
}
