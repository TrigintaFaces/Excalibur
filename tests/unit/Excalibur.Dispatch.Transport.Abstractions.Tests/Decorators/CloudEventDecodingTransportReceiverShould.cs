// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

/// <summary>
/// Binds the three observable outcomes of inbound CloudEvents decoding, and the batch boundary that keeps
/// one of them from destroying the other two.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decorator attaches; it never substitutes.</b> Every arm asserts against the message the caller
/// already holds, because replacing it would break every consumer reading the transport's own fields.
/// </para>
/// <para>
/// <b>Why the batch arm is the load-bearing one.</b> The decorator wraps every receiver on every transport,
/// so a malformed message belonging to one publisher arrives beside messages belonging to consumers who
/// never opted into CloudEvents. An implementation that let the decoder's exception escape would fail
/// delivery for all of them, and would pass every other arm in this class.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventDecodingTransportReceiverShould
{
    private static readonly byte[] PayloadBytes = Encoding.UTF8.GetBytes("the original payload");

    /// <summary>
    /// LIVENESS. A decoded event is published under its key and nothing else about the message moves.
    /// </summary>
    [Fact]
    public async Task Publish_the_decoded_event_and_leave_the_message_otherwise_untouched()
    {
        var message = MessageWithPayload("m-1");
        var decoded = Event("order.placed", "evt-1");
        var receiver = Decorate([message], _ => decoded);

        var received = await receiver.ReceiveAsync(10, CancellationToken.None);

        var subject = received.ShouldHaveSingleItem();
        subject.ShouldBeSameAs(message,
            "the decorator attaches to the received message rather than replacing it");
        subject.ProviderData[CloudEventDecodingTransportReceiver.CloudEventProviderDataKey]
            .ShouldBeSameAs(decoded);
        subject.ProviderData.ShouldNotContainKey(
            CloudEventDecodingTransportReceiver.CloudEventErrorProviderDataKey,
            "a successful decode must not also report an error");
        subject.Body.ToArray().ShouldBe(PayloadBytes,
            "the payload is the consumer's, not the decorator's, and must survive decoding untouched");
    }

    /// <summary>
    /// LIVENESS, and the arm that keeps the decorator honest on transports nobody opted in on: a message
    /// carrying no CloudEvents markers is left completely unmarked.
    /// </summary>
    /// <remarks>
    /// "Not a CloudEvent" is a normal outcome on a shared transport, not a failure. If this arm ever
    /// asserted an error key the decorator would be reporting a fault for ordinary traffic.
    /// </remarks>
    [Fact]
    public async Task Leave_a_message_carrying_no_markers_completely_unmarked()
    {
        var message = MessageWithPayload("m-1");
        var receiver = Decorate([message], _ => null);

        var received = await receiver.ReceiveAsync(10, CancellationToken.None);

        var subject = received.ShouldHaveSingleItem();
        subject.ShouldBeSameAs(message);
        subject.ProviderData.ShouldBeEmpty(
            "pass-through is the absence of BOTH keys - an unmarked message is indistinguishable from one "
            + "the decorator never saw, which is exactly the contract");
        subject.Body.ToArray().ShouldBe(PayloadBytes);
    }

    /// <summary>
    /// SAFETY. A malformed event is reported rather than thrown, and the reason travels with the message.
    /// </summary>
    /// <remarks>
    /// Both halves matter. Not throwing is what stops one bad publisher stalling a shared queue; publishing
    /// the reason is what stops a corrupt event being indistinguishable from ordinary non-CloudEvents
    /// traffic - the silent downgrade the decorator exists to prevent.
    /// </remarks>
    [Fact]
    public async Task Report_a_malformed_event_on_the_message_instead_of_throwing()
    {
        var message = MessageWithPayload("m-1");
        var refusal = new InvalidOperationException("markers present but incomplete");
        var receiver = Decorate([message], _ => throw refusal);

        // Awaited directly rather than wrapped: if the decoder's refusal escapes, this arm fails carrying
        // the original exception, which is the diagnostic the next reader wants.
        var received = await receiver.ReceiveAsync(10, CancellationToken.None);

        var subject = received.ShouldHaveSingleItem();
        subject.ProviderData[CloudEventDecodingTransportReceiver.CloudEventErrorProviderDataKey]
            .ShouldBeSameAs(refusal, "the consumer needs the reason, not merely the fact of failure");
        subject.ProviderData.ShouldNotContainKey(
            CloudEventDecodingTransportReceiver.CloudEventProviderDataKey,
            "a failed decode must not also publish an event - the two states are mutually exclusive");
    }

    /// <summary>
    /// SAFETY, and the arm this class exists for: one malformed message must not deny delivery to the
    /// messages that happened to arrive beside it.
    /// </summary>
    /// <remarks>
    /// This is the arm that fails against an implementation which lets the decoder's exception escape.
    /// Every other arm in this class passes against that implementation, because every other arm uses a
    /// single-message batch.
    /// </remarks>
    [Fact]
    public async Task Deliver_and_decode_the_messages_that_arrived_beside_a_malformed_one()
    {
        var first = MessageWithPayload("m-1");
        var poison = MessageWithPayload("m-2");
        var last = MessageWithPayload("m-3");
        var decoded = Event("order.placed", "evt-1");

        var receiver = Decorate(
            [first, poison, last],
            message => message.Id == "m-2"
                ? throw new JsonException("structured body is not valid JSON")
                : decoded);

        var received = await receiver.ReceiveAsync(10, CancellationToken.None);

        received.Count.ShouldBe(3, "the whole batch is delivered - a bad message is marked, never dropped");
        first.ProviderData[CloudEventDecodingTransportReceiver.CloudEventProviderDataKey]
            .ShouldBeSameAs(decoded, "a message BEFORE the malformed one still decodes");
        last.ProviderData[CloudEventDecodingTransportReceiver.CloudEventProviderDataKey]
            .ShouldBeSameAs(decoded, "a message AFTER the malformed one still decodes");
        poison.ProviderData.ShouldContainKey(
            CloudEventDecodingTransportReceiver.CloudEventErrorProviderDataKey);
    }

    /// <summary>
    /// SAFETY. A fault that is not a malformed-input outcome propagates instead of being recorded as one.
    /// </summary>
    /// <remarks>
    /// The pair to the malformed arm above, and the reason the decorator enumerates the exceptions it
    /// catches rather than catching broadly. A broad catch passes the malformed arm and turns a transport
    /// fault, an out-of-memory or a cancellation into a per-message annotation on an otherwise successful
    /// receive - hiding a real failure behind a green batch.
    /// </remarks>
    [Fact]
    public async Task Let_a_transport_fault_propagate_rather_than_recording_it_as_a_decode_error()
    {
        var message = MessageWithPayload("m-1");
        var receiver = Decorate([message], _ => throw new TimeoutException("the broker went away"));

        _ = await Should.ThrowAsync<TimeoutException>(
            () => receiver.ReceiveAsync(10, CancellationToken.None));

        message.ProviderData.ShouldBeEmpty(
            "a transport fault is not a malformed CloudEvent and must not be annotated as one");
    }

    /// <summary>
    /// SAFETY. Cancellation is the caller stopping, not a bad message, and must not be swallowed.
    /// </summary>
    /// <remarks>
    /// Swallowing it would let the decorator carry on decoding past a cancelled receive.
    /// </remarks>
    [Fact]
    public async Task Let_cancellation_propagate_rather_than_recording_it_as_a_decode_error()
    {
        var message = MessageWithPayload("m-1");
        var receiver = Decorate([message], _ => throw new OperationCanceledException());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => receiver.ReceiveAsync(10, CancellationToken.None));

        message.ProviderData.ShouldBeEmpty();
    }

    /// <summary>
    /// LIVENESS for the wiring: the public entry point actually inserts the decorator.
    /// </summary>
    /// <remarks>
    /// The mutation this arm binds is the wiring-severed one - a transport registration that stops calling
    /// <c>WithCloudEventDecoding</c> leaves every type in the assembly present and every other arm in this
    /// class green, while no message on that transport is ever decoded.
    /// </remarks>
    [Fact]
    public void Insert_the_decoding_decorator_when_the_receiver_opts_in()
    {
        var inner = A.Fake<ITransportReceiver>();

        var built = inner.WithCloudEventDecoding(CloudEventBinding.Amqp10);

        _ = built.ShouldBeOfType<CloudEventDecodingTransportReceiver>(
            "a receiver that opts in but is not wrapped decodes nothing, silently");
    }

    /// <summary>
    /// REGRESSION, at the entry point a CONSUMER reaches. Ordinary traffic whose properties happen to be
    /// named <c>id</c> and <c>type</c> must arrive unmarked, not carrying a decode-error annotation.
    /// </summary>
    /// <remarks>
    /// This arm uses the REAL decoder rather than a stub, because the behaviour under test is the
    /// decoder's detection rule and a stub cannot exhibit it.
    /// <para>
    /// It is the twin of the decoder-level arm and states the requirement in the terms a consumer
    /// experiences. The decoder-level arm alone would be satisfied by a remedy that leaves the decoder
    /// refusing while this decorator quietly annotates — which is the defect rather than a fix.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Deliver_ordinary_traffic_named_id_and_type_without_a_decode_error()
    {
        var message = new TransportReceivedMessage
        {
            Id = "m-1",
            Body = Encoding.UTF8.GetBytes("an ordinary business message"),
            Properties = new Dictionary<string, object>
            {
                ["id"] = "order-4711",
                ["type"] = "application/vnd.orders.v2+json",
            },
        };

        var inner = A.Fake<ITransportReceiver>();
        _ = A.CallTo(() => inner.ReceiveAsync(A<int>._, A<CancellationToken>._))
            .Returns<IReadOnlyList<TransportReceivedMessage>>([message]);

        var receiver = new CloudEventDecodingTransportReceiver(
            inner, new TransportReceivedMessageCloudEventDecoder(CloudEventBinding.Amqp10));

        var received = await receiver.ReceiveAsync(10, CancellationToken.None);

        _ = received.ShouldHaveSingleItem();
        message.ProviderData.ShouldNotContainKey(
            CloudEventDecodingTransportReceiver.CloudEventErrorProviderDataKey,
            "a business message whose properties are named id and type is ordinary traffic; annotating it "
            + "as a failed CloudEvent decode tells the consumer their own message is corrupt");
    }

    private static CloudEvent Event(string type, string id) =>
        new() { Id = id, Type = type, Source = new Uri("https://example.test/orders") };

    private static TransportReceivedMessage MessageWithPayload(string id) =>
        new() { Id = id, Body = PayloadBytes };

    private static CloudEventDecodingTransportReceiver Decorate(
        IReadOnlyList<TransportReceivedMessage> batch,
        Func<TransportReceivedMessage, CloudEvent?> decode)
    {
        var inner = A.Fake<ITransportReceiver>();
        _ = A.CallTo(() => inner.ReceiveAsync(A<int>._, A<CancellationToken>._)).Returns(batch);

        return new CloudEventDecodingTransportReceiver(inner, new StubDecoder(decode));
    }

    /// <summary>
    /// Implements the decoder contract DIRECTLY - no first-party base supplies the member under test, so
    /// these arms bind the interface's own requirement rather than re-testing a shared base class.
    /// </summary>
    private sealed class StubDecoder(Func<TransportReceivedMessage, CloudEvent?> decode)
        : ICloudEventDecoder<TransportReceivedMessage>
    {
        public Task<CloudEvent?> TryDecodeAsync(
            TransportReceivedMessage transportMessage,
            CancellationToken cancellationToken) =>
            Task.FromResult(decode(transportMessage));
    }
}
