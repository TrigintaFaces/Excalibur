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
/// Review-fix lock (S862 REVIEW B1) for the m084l4 RabbitMQ receive-ingress payload guard.
/// </summary>
/// <remarks>
/// The guard must reject an over-limit message <b>without stranding the batch or poison-looping</b>:
/// the oversized message is nacked (requeue:false → DLQ if configured) and skipped, valid messages in the
/// same <c>BasicGetAsync</c> batch are still returned, and one poison message does not abort
/// <see cref="RabbitMqTransportReceiver.ReceiveAsync"/>. The pre-fix impl ran the guard BEFORE caching the
/// delivery tag and let <see cref="PayloadTooLargeException"/> propagate to the outer catch (rethrow) — so
/// the oversized tag was never cached (uncancellable → redelivered → unbounded poison loop) and the whole
/// batch was stranded unacked. These are author≠impl locks (RED on the pre-fix code, GREEN once the guard
/// nacks-inside-the-loop-and-continues, mirroring the outbox side).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqReceiverPayloadGuardShould
{
    private const string Queue = "test-queue";
    private const int MaxPayloadBytes = 8;

    private static BasicGetResult GetResult(ulong deliveryTag, byte[] body) =>
        new(
            deliveryTag: deliveryTag,
            redelivered: false,
            exchange: "ex",
            routingKey: "rk",
            messageCount: 0,
            basicProperties: new RabbitMqBasicProperties { MessageId = $"msg-{deliveryTag}" },
            body: body);

    private static RabbitMqTransportReceiver CreateReceiver(IChannel channel) =>
        new(channel, Queue, Queue, NullLogger<RabbitMqTransportReceiver>.Instance, MaxPayloadBytes);

    [Fact]
    public async Task NackAndSkipOversizedMessage_ThenReturnValidMessagesInSameBatch()
    {
        // Batch: [oversized (20B > 8B limit), valid (4B), empty] — the oversized must be nacked+skipped,
        // the valid one still returned, and ReceiveAsync must NOT throw.
        var channel = A.Fake<IChannel>();
        var oversized = GetResult(1, Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ")); // 20 bytes
        var valid = GetResult(2, Encoding.UTF8.GetBytes("ok!!"));                      // 4 bytes

        var call = 0;
        A.CallTo(() => channel.BasicGetAsync(Queue, false, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                return call switch
                {
                    1 => Task.FromResult<BasicGetResult?>(oversized),
                    2 => Task.FromResult<BasicGetResult?>(valid),
                    _ => Task.FromResult<BasicGetResult?>(null),
                };
            });

        var receiver = CreateReceiver(channel);

        var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

        // Valid message returned; oversized excluded.
        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("msg-2");

        // Oversized message rejected (nack, requeue:false → DLQ), NOT left unsettled to poison-loop.
        A.CallTo(() => channel.BasicNackAsync(1, false, false, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task NotThrow_WhenTheOnlyMessageIsOversized()
    {
        // A single poison message must not abort ReceiveAsync — it returns empty, having nacked the poison.
        var channel = A.Fake<IChannel>();
        var oversized = GetResult(7, Encoding.UTF8.GetBytes("way-too-large-payload"));

        var call = 0;
        A.CallTo(() => channel.BasicGetAsync(Queue, false, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                return call == 1
                    ? Task.FromResult<BasicGetResult?>(oversized)
                    : Task.FromResult<BasicGetResult?>(null);
            });

        var receiver = CreateReceiver(channel);

        var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

        messages.ShouldBeEmpty();
        A.CallTo(() => channel.BasicNackAsync(7, false, false, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task ReturnMessage_WhenWithinLimit()
    {
        // Control: an at/under-limit message passes through unchanged (guard non-vacuous — it does not reject valid).
        var channel = A.Fake<IChannel>();
        var valid = GetResult(3, Encoding.UTF8.GetBytes("12345678")); // exactly 8 bytes = at limit

        var call = 0;
        A.CallTo(() => channel.BasicGetAsync(Queue, false, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                return call == 1
                    ? Task.FromResult<BasicGetResult?>(valid)
                    : Task.FromResult<BasicGetResult?>(null);
            });

        var receiver = CreateReceiver(channel);

        var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("msg-3");
        A.CallTo(() => channel.BasicNackAsync(A<ulong>._, A<bool>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
