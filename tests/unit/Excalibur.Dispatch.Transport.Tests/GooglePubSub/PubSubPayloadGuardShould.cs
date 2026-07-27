// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Author≠impl ingress payload-guard locks (bead ydvyi7) for BOTH Google Pub/Sub surfaces — the pull
/// <see cref="PubSubTransportReceiver"/> and the push <see cref="PubSubTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: an over-limit payload is rejected via
/// <c>PayloadSizeGuard.EnsureWithinLimit(pubsubMessage.Data.Length, ...)</c> in the convert method BEFORE
/// the body (<c>Data.Memory</c>) is materialized, throwing <c>PayloadTooLargeException</c>. The receive
/// loop catches it: the pull receiver NACKs the single oversized message (dead-lettered by policy if a
/// policy is configured) and continues the batch so valid messages still flow; the push subscriber
/// returns <see cref="SubscriberClient.Reply.Nack"/> without ever invoking the consumer handler. Non-vacuous:
/// a <see langword="null"/> limit opts out (unbounded) and the oversized payload is accepted — the
/// differential vs the enforced path is what proves the guard is wired on both surfaces. The boundary is
/// inclusive (N bytes OK). Guard measures the raw <see cref="ByteString"/> length, so oversized/small are
/// built with <see cref="ByteString.CopyFromUtf8(string)"/> and MaxPayloadBytes = 8.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubPayloadGuardShould
{
    private const string Subscription = "projects/test/subscriptions/test-sub";
    private const int MaxPayloadBytes = 8;

    private static PubsubMessage PubSubMessage(string id, string body) =>
        new() { MessageId = id, Data = ByteString.CopyFromUtf8(body) };

    private static ReceivedMessage Received(string id, string body) =>
        new() { AckId = $"ack-{id}", Message = PubSubMessage(id, body) };

    private static PullResponse Batch(params ReceivedMessage[] messages)
    {
        var response = new PullResponse();
        response.ReceivedMessages.AddRange(messages);
        return response;
    }

    private static PubSubTransportReceiver CreateReceiver(ISubscriberApiClientSeam client, int? max = MaxPayloadBytes, bool hasDeadLetterPolicy = false) =>
        new(client, Subscription, NullLogger<PubSubTransportReceiver>.Instance, maxPayloadBytes: max, hasDeadLetterPolicy: hasDeadLetterPolicy);

    private static PubSubTransportSubscriber CreateSubscriber(ISubscriberClientSeam client, int? max = MaxPayloadBytes, bool hasDeadLetterPolicy = false) =>
        new(client, Subscription, NullLogger<PubSubTransportSubscriber>.Instance, maxPayloadBytes: max, hasDeadLetterPolicy: hasDeadLetterPolicy);

    // ---- Pull surface: PubSubTransportReceiver.ReceiveAsync ----

    [Fact]
    public async Task Receiver_RejectOversized_ReturnValidInSameBatch()
    {
        var client = A.Fake<ISubscriberApiClientSeam>();
        var oversized = Received("m1", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = Received("m2", "ok!!");                     // 4 bytes
        A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(oversized, valid)));

        var messages = await CreateReceiver(client, hasDeadLetterPolicy: true).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m2");
        // ujrnr4: the oversized poison message is NACKed (ack deadline set to 0), NOT acked — a Pub/Sub
        // dead-letter policy only routes a message that exhausts delivery attempts, never an acked one.
        // Nacking lets the policy dead-letter it (converges with the streaming subscriber's nack-on-oversized).
        A.CallTo(() => client.ModifyAckDeadlineAsync(
            Subscription, A<IEnumerable<string>>.That.Matches(ids => ids.Contains("ack-m1")), 0, A<CancellationToken>._))
            .MustHaveHappened();
        // Must NOT silently discard the poison message by acking it.
        A.CallTo(() => client.AcknowledgeAsync(
            Subscription, A<IEnumerable<string>>.That.Matches(ids => ids.Contains("ack-m1")), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_AcceptMessage_AtExactLimit()
    {
        var client = A.Fake<ISubscriberApiClientSeam>();
        A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(Received("m3", "12345678")))); // exactly 8 bytes

        var messages = await CreateReceiver(client).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m3");
        A.CallTo(() => client.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_NotEnforce_WhenLimitNull()
    {
        var client = A.Fake<ISubscriberApiClientSeam>();
        A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(Received("m4", "way-too-large-for-eight-bytes"))));

        var messages = await CreateReceiver(client, max: null).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m4");
        A.CallTo(() => client.AcknowledgeAsync(A<string>._, A<IEnumerable<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    // ---- Push surface: PubSubTransportSubscriber.SubscribeAsync ----

    [Fact]
    public async Task Subscriber_RejectOversized_NeverHandsItToHandler()
    {
        var client = A.Fake<ISubscriberClientSeam>();
        Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>? sdkHandler = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => client.StartAsync(A<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>._))
            .Invokes(call =>
            {
                sdkHandler = call.GetArgument<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>(0);
                started.TrySetResult();
            })
            .Returns(Task.CompletedTask);

        var handled = new List<string>();
        using var cts = new CancellationTokenSource();
        var subscribeTask = CreateSubscriber(client, hasDeadLetterPolicy: true).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        (await WaitAsync(started.Task)).ShouldBeTrue("subscriber should register its handler");

        // Oversized: rejected (nack) before the handler is ever invoked.
        var oversizedReply = await sdkHandler!(PubSubMessage("s1", "0123456789ABCDEFGHIJ"), CancellationToken.None);
        oversizedReply.ShouldBe(SubscriberClient.Reply.Nack);

        // Valid: passes the guard and reaches the handler.
        var validReply = await sdkHandler!(PubSubMessage("s2", "ok!!"), CancellationToken.None);
        validReply.ShouldBe(SubscriberClient.Reply.Ack);

        handled.ShouldBe(["s2"]); // oversized never deserialized/handled

        await cts.CancelAsync();
        await subscribeTask;
    }

    [Fact]
    public async Task Subscriber_NotEnforce_WhenLimitNull()
    {
        var client = A.Fake<ISubscriberClientSeam>();
        Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>? sdkHandler = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => client.StartAsync(A<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>._))
            .Invokes(call =>
            {
                sdkHandler = call.GetArgument<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>(0);
                started.TrySetResult();
            })
            .Returns(Task.CompletedTask);

        var handled = new List<string>();
        using var cts = new CancellationTokenSource();
        var subscribeTask = CreateSubscriber(client, max: null).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        (await WaitAsync(started.Task)).ShouldBeTrue("subscriber should register its handler");

        // Opt-out: oversized still passes the (disabled) guard and reaches the handler.
        var reply = await sdkHandler!(PubSubMessage("s3", "way-too-large-for-eight-bytes"), CancellationToken.None);
        reply.ShouldBe(SubscriberClient.Reply.Ack);
        handled.ShouldBe(["s3"]);

        await cts.CancelAsync();
        await subscribeTask;
    }

    private static async Task<bool> WaitAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        return completed == task;
    }
}
