// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Author≠impl ingress payload-guard lock (S863 bead wr3tuo) for the RabbitMQ <b>push</b> surface —
/// <see cref="RabbitMqTransportSubscriber.SubscribeAsync"/> — the reference-transport Subscriber gap
/// (the pull <see cref="RabbitMqTransportReceiver"/> surface is already covered by
/// <c>RabbitMqReceiverPayloadGuardShould</c>).
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: <c>PayloadSizeGuard.EnsureWithinLimit(args.Body.Length, …)</c>
/// runs in <c>ConvertToReceivedMessage</c> BEFORE the body is materialized. An over-limit delivery throws
/// <c>PayloadTooLargeException</c>, which the consumer callback catches to nack the poison message
/// (<c>BasicNackAsync(requeue: false)</c> → DLX) and return WITHOUT invoking the handler — so it cannot
/// loop and never reaches business logic; a valid delivery is still handled + acked. Non-vacuous: RED
/// against the missing-guard mutant (remove the <c>EnsureWithinLimit</c> line → oversized is handled, no
/// nack), proven by the null-limit opt-out differential (unbounded ⇒ oversized IS handled). The boundary
/// is inclusive (exactly N bytes accepted).
///
/// The push loop is driven deterministically by capturing the registered <see cref="IAsyncBasicConsumer"/>
/// from the faked <c>BasicConsumeAsync</c> and awaiting <c>HandleBasicDeliverAsync</c> per delivery (which
/// runs the callback + settlement to completion), then cancelling to unblock the infinite delay.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqSubscriberPayloadGuardShould
{
    private const string Queue = "test-queue";
    private const int MaxPayloadBytes = 8;

    private static RabbitMqTransportSubscriber CreateSubscriber(IChannel channel, int? max = MaxPayloadBytes) =>
        new(channel, Queue, Queue, NullLogger<RabbitMqTransportSubscriber>.Instance,
            prefetchCount: 0, prefetchGlobal: false, maxPayloadBytes: max);

    private static RabbitMqBasicProperties Props(ulong deliveryTag) =>
        new() { MessageId = $"msg-{deliveryTag}" };

    // Captures the consumer the subscriber registers, so deliveries can be driven deterministically.
    private static IAsyncBasicConsumer SetUpCapture(IChannel channel, TaskCompletionSource<IAsyncBasicConsumer> ready)
    {
        A.CallTo(() => channel.BasicConsumeAsync(
                A<string>._, A<bool>._, A<string>._, A<bool>._, A<bool>._,
                A<IDictionary<string, object?>>._, A<IAsyncBasicConsumer>._, A<CancellationToken>._))
            .Invokes(call => ready.TrySetResult((IAsyncBasicConsumer)call.Arguments[6]!))
            .Returns(Task.FromResult("consumer-tag"));
        A.CallTo(() => channel.BasicCancelAsync(A<string>._, A<bool>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);
        return null!;
    }

    private static Task DeliverAsync(IAsyncBasicConsumer consumer, ulong deliveryTag, byte[] body) =>
        consumer.HandleBasicDeliverAsync(
            "consumer-tag", deliveryTag, redelivered: false, exchange: "ex", routingKey: "rk",
            properties: Props(deliveryTag), body: body, cancellationToken: CancellationToken.None);

    [Fact]
    public async Task RejectOversized_NeverHandsItToHandler_ThenHandleValidDelivery()
    {
        var channel = A.Fake<IChannel>();
        var ready = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = SetUpCapture(channel, ready);

        var handled = new List<string>();
        using var cts = new CancellationTokenSource();

        var subscribe = CreateSubscriber(channel).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        var consumer = await ready.Task;

        // Oversized (20B > 8B limit) is nacked+skipped; the valid (4B) delivery IS handled.
        await DeliverAsync(consumer, 1, Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ"));
        await DeliverAsync(consumer, 2, Encoding.UTF8.GetBytes("ok!!"));

        cts.Cancel();
        await subscribe;

        // Oversized never deserialized/handled; only the valid message reached the handler.
        handled.ShouldBe(["msg-2"]);
        // Oversized poison-rejected to the DLX (no requeue) so it cannot loop.
        A.CallTo(() => channel.BasicNackAsync(1, false, false, A<CancellationToken>._)).MustHaveHappened();
        // Valid delivery acked.
        A.CallTo(() => channel.BasicAckAsync(2, false, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task HandleDelivery_AtExactLimit()
    {
        // Control: an at-limit (exactly 8B) delivery passes the guard and is handled (guard non-vacuous —
        // it does not reject valid, boundary inclusive).
        var channel = A.Fake<IChannel>();
        var ready = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = SetUpCapture(channel, ready);

        var handled = new List<string>();
        using var cts = new CancellationTokenSource();

        var subscribe = CreateSubscriber(channel).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        var consumer = await ready.Task;

        await DeliverAsync(consumer, 3, Encoding.UTF8.GetBytes("12345678")); // exactly 8 bytes = at limit

        cts.Cancel();
        await subscribe;

        handled.ShouldBe(["msg-3"]);
        A.CallTo(() => channel.BasicNackAsync(A<ulong>._, A<bool>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task NotEnforce_WhenLimitNull()
    {
        // Differential (proves non-vacuity): with the limit opted out, the same oversized payload IS
        // handled and never nacked — so the enforced-case rejection above is the guard, not the harness.
        var channel = A.Fake<IChannel>();
        var ready = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = SetUpCapture(channel, ready);

        var handled = new List<string>();
        using var cts = new CancellationTokenSource();

        var subscribe = CreateSubscriber(channel, max: null).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        var consumer = await ready.Task;

        await DeliverAsync(consumer, 9, Encoding.UTF8.GetBytes("way-too-large-payload"));

        cts.Cancel();
        await subscribe;

        handled.ShouldBe(["msg-9"]); // opt-out: oversized still handled, no ingress rejection
        A.CallTo(() => channel.BasicNackAsync(9, false, false, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
