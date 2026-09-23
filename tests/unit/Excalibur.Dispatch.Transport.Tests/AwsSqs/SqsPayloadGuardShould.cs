// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Author≠impl ingress payload-guard locks (bead ydvyi7) for BOTH AwsSqs surfaces — the pull
/// <see cref="SqsTransportReceiver"/> and the push <see cref="SqsTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: an over-limit body is rejected via
/// <c>PayloadSizeGuard.EnsureWithinLimit</c> in the convert method BEFORE the body is materialized, while
/// valid messages in the same batch still flow. Non-vacuous: RED against the missing-guard mutant (remove
/// the <c>EnsureWithinLimit</c> line → oversized is returned/handled), GREEN with the guard wired on both
/// surfaces. A <see langword="null"/> limit opts out (unbounded); the boundary is inclusive (N bytes OK).
/// <para>
/// <b>CORRECTED (bead rxb2d7): deleting is NOT redrive, and this file previously said it was.</b> The
/// remarks and two arms below described the oversized message as "poison-deleted (SQS redrive → DLQ)".
/// <c>DeleteMessage</c> removes the message outright; redrive is driven by the receive count of a message
/// that becomes visible again, so a deleted message never reaches a dead-letter queue. How an oversized
/// payload is settled now depends on whether a DLQ exists to catch it — see
/// <c>SqsPoisonPayloadSettlement</c> — and both surfaces route through that one decision so they cannot
/// diverge. The delete-asserting arms below are therefore the NO-DLQ case, and are named so.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsPayloadGuardShould
{
    private const string QueueUrl = "https://sqs.test/queue";
    private const int MaxPayloadBytes = 8;

    private static Message SqsMessage(string id, string body) =>
        new() { MessageId = id, Body = body, ReceiptHandle = $"rh-{id}" };

    private static ReceiveMessageResponse Batch(params Message[] messages) =>
        new() { Messages = [.. messages] };

    private static SqsTransportReceiver CreateReceiver(
        IAmazonSQS sqs, int? max = MaxPayloadBytes, bool hasDeadLetterQueue = false) =>
        new(sqs, QueueUrl, NullLogger<SqsTransportReceiver>.Instance, maxPayloadBytes: max,
            hasDeadLetterQueue: hasDeadLetterQueue);

    private static SqsTransportSubscriber CreateSubscriber(
        IAmazonSQS sqs, int? max = MaxPayloadBytes, bool hasDeadLetterQueue = false) =>
        new(sqs, QueueUrl, QueueUrl, new AwsSqsVisibilityHeartbeatOptions { Enabled = false },
            NullLogger<SqsTransportSubscriber>.Instance, maxPayloadBytes: max,
            hasDeadLetterQueue: hasDeadLetterQueue);

    // ---- Pull surface: SqsTransportReceiver.ReceiveAsync ----

    [Fact]
    public async Task Receiver_RejectOversized_ReturnValidInSameBatch()
    {
        var sqs = A.Fake<IAmazonSQS>();
        var oversized = SqsMessage("m1", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = SqsMessage("m2", "ok!!");                     // 4 bytes
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(oversized, valid)));

        var messages = await CreateReceiver(sqs).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m2");
        // NO dead-letter queue is configured on this fixture, so dropping is correct: nothing would
        // catch a redriven message and leaving it would loop forever. Never returned, never deserialized.
        A.CallTo(() => sqs.DeleteMessageAsync(
            A<DeleteMessageRequest>.That.Matches(r => r.ReceiptHandle == "rh-m1"), A<CancellationToken>._))
            .MustHaveHappened();
    }

    /// <summary>
    /// SAFETY, bead rxb2d7. With a dead-letter queue configured, an oversized message must NOT be
    /// deleted — deleting destroys the one copy the DLQ was configured to preserve.
    /// </summary>
    /// <remarks>
    /// Doing nothing is the action: the visibility timeout expires, the receive count advances, and SQS
    /// redrive moves the message to the dead-letter queue after <c>maxReceiveCount</c>. The previous code
    /// deleted unconditionally while its comment claimed redrive handled it, so a consumer who had
    /// configured a DLQ lost accepted durable work on first receive — under nothing worse than a producer
    /// sending a broker-valid payload larger than this consumer's <c>MaxPayloadBytes</c>.
    /// </remarks>
    [Fact]
    public async Task Receiver_PreserveOversizedForRedrive_WhenDeadLetterQueueConfigured()
    {
        var sqs = A.Fake<IAmazonSQS>();
        var oversized = SqsMessage("m5", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = SqsMessage("m6", "ok!!");                     // 4 bytes
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(oversized, valid)));

        var messages = await CreateReceiver(sqs, hasDeadLetterQueue: true).ReceiveAsync(10, CancellationToken.None);

        // Still not handed to the caller, and still never deserialized - the guard is unchanged.
        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m6");

        // The whole point: it survives, so redrive can move it to the DLQ.
        A.CallTo(() => sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_AcceptMessage_AtExactLimit()
    {
        var sqs = A.Fake<IAmazonSQS>();
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(SqsMessage("m3", "12345678")))); // exactly 8 bytes

        var messages = await CreateReceiver(sqs).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m3");
        A.CallTo(() => sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_NotEnforce_WhenLimitNull()
    {
        var sqs = A.Fake<IAmazonSQS>();
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Batch(SqsMessage("m4", "way-too-large-for-eight-bytes"))));

        var messages = await CreateReceiver(sqs, max: null).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m4");
        A.CallTo(() => sqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    // ---- Push surface: SqsTransportSubscriber.SubscribeAsync ----

    [Fact]
    public async Task Subscriber_RejectOversized_NeverHandsItToHandler()
    {
        var sqs = A.Fake<IAmazonSQS>();
        var oversized = SqsMessage("s1", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = SqsMessage("s2", "ok!!");                     // 4 bytes

        using var cts = new CancellationTokenSource();
        var call = 0;
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                if (call == 1)
                {
                    return Task.FromResult(Batch(oversized, valid));
                }

                cts.Cancel(); // stop the loop after the first batch is settled
                return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
            });
        A.CallTo(() => sqs.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new DeleteMessageBatchResponse()));

        var handled = new List<string>();
        await CreateSubscriber(sqs).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        // Oversized never deserialized/handled; only the valid message reached the handler.
        handled.ShouldBe(["s2"]);
        // NO dead-letter queue on this fixture, so dropping is correct - proven by its receipt handle
        // appearing in the delete batch.
        A.CallTo(() => sqs.DeleteMessageBatchAsync(
            A<DeleteMessageBatchRequest>.That.Matches(r => r.Entries.Exists(e => e.ReceiptHandle == "rh-s1")),
            A<CancellationToken>._)).MustHaveHappened();
    }

    /// <summary>
    /// SAFETY, bead rxb2d7 — the push surface. The subscriber carried the identical unconditional delete,
    /// so fixing only the pull receiver would have left the same queue losing messages on the other path.
    /// </summary>
    [Fact]
    public async Task Subscriber_PreserveOversizedForRedrive_WhenDeadLetterQueueConfigured()
    {
        var sqs = A.Fake<IAmazonSQS>();
        var oversized = SqsMessage("s4", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = SqsMessage("s5", "ok!!");                     // 4 bytes

        using var cts = new CancellationTokenSource();
        var call = 0;
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                if (call == 1)
                {
                    return Task.FromResult(Batch(oversized, valid));
                }

                cts.Cancel();
                return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
            });
        A.CallTo(() => sqs.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new DeleteMessageBatchResponse()));

        var handled = new List<string>();
        await CreateSubscriber(sqs, hasDeadLetterQueue: true).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        handled.ShouldBe(["s5"]);

        // The oversized message must NOT be in any delete batch - the acknowledged valid message still is,
        // so asserting on the specific receipt handle rather than on "no delete happened at all" is what
        // makes this arm about the poison message instead of about batching.
        A.CallTo(() => sqs.DeleteMessageBatchAsync(
            A<DeleteMessageBatchRequest>.That.Matches(r => r.Entries.Exists(e => e.ReceiptHandle == "rh-s4")),
            A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Subscriber_NotEnforce_WhenLimitNull()
    {
        var sqs = A.Fake<IAmazonSQS>();
        using var cts = new CancellationTokenSource();
        var call = 0;
        A.CallTo(() => sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                call++;
                if (call == 1)
                {
                    return Task.FromResult(Batch(SqsMessage("s3", "way-too-large-for-eight-bytes")));
                }

                cts.Cancel();
                return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
            });
        A.CallTo(() => sqs.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new DeleteMessageBatchResponse()));

        var handled = new List<string>();
        await CreateSubscriber(sqs, max: null).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        handled.ShouldBe(["s3"]); // opt-out: oversized still handled, no ingress rejection
    }
}
