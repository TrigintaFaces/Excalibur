// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Author≠impl ingress payload-guard locks (bead ydvyi7) for BOTH Kafka surfaces — the pull
/// <see cref="KafkaTransportReceiver"/> and the push <see cref="KafkaTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: an over-limit body is rejected via
/// <c>PayloadSizeGuard.EnsureWithinLimit(consumeResult.Message.Value.Length, ...)</c> in the convert
/// method BEFORE the body is materialized. Because an oversized message left uncommitted would be
/// redelivered forever (a poison loop that stalls the partition), both surfaces catch the resulting
/// <c>PayloadTooLargeException</c> and <c>Commit</c> PAST the offending message (offset + 1) to skip it —
/// mirroring the non-requeue reject path — while valid messages in the same batch still flow. Non-vacuous:
/// RED against the missing-guard mutant (remove the <c>EnsureWithinLimit</c> line → oversized is
/// returned/handled and never committed-past), GREEN with the guard wired on both surfaces. A
/// <see langword="null"/> limit opts out (unbounded); the boundary is inclusive (N bytes OK).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaPayloadGuardShould
{
    private const string Topic = "orders-topic";
    private const int MaxPayloadBytes = 8;

    private static KafkaConsumeResult Record(string key, int payloadBytes, long offset) =>
        new()
        {
            Topic = Topic,
            Partition = new Partition(0),
            Offset = new Offset(offset),
            Message = new Message<string, byte[]> { Key = key, Value = new byte[payloadBytes] },
        };

    private static KafkaTransportReceiver CreateReceiver(IConsumer<string, byte[]> consumer, int? max = MaxPayloadBytes) =>
        new(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, maxPayloadBytes: max);

    private static KafkaTransportSubscriber CreateSubscriber(IConsumer<string, byte[]> consumer, int? max = MaxPayloadBytes) =>
        new(consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, maxPayloadBytes: max);

    private static bool CommitsPast(IEnumerable<TopicPartitionOffset> offsets, long expected) =>
        offsets.Any(o => o.Offset.Value == expected);

    // ---- Pull surface: KafkaTransportReceiver.ReceiveAsync ----

    [Fact]
    public async Task Receiver_RejectOversized_ReturnValidInSameBatch()
    {
        var consumer = A.Fake<IConsumer<string, byte[]>>();
        var oversized = Record("m1", 20, offset: 5); // 20 bytes > 8
        var valid = Record("m2", 4, offset: 6);      // 4 bytes
        var seq = new Queue<KafkaConsumeResult?>([oversized, valid, null]);
        A.CallTo(() => consumer.Consume(A<TimeSpan>._)).ReturnsLazily(() => seq.Dequeue());

        var messages = await CreateReceiver(consumer).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m2");
        // Oversized poison-skipped: committed PAST it (offset 5 + 1 = 6) so it can never redeliver.
        A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>.That.Matches(o => CommitsPast(o, 6))))
            .MustHaveHappened();
    }

    [Fact]
    public async Task Receiver_AcceptMessage_AtExactLimit()
    {
        var consumer = A.Fake<IConsumer<string, byte[]>>();
        var seq = new Queue<KafkaConsumeResult?>([Record("m3", 8, offset: 3), null]); // exactly 8 bytes
        A.CallTo(() => consumer.Consume(A<TimeSpan>._)).ReturnsLazily(() => seq.Dequeue());

        var messages = await CreateReceiver(consumer).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m3");
        // Within the limit: not skipped, so no commit-past during receive.
        A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_NotEnforce_WhenLimitNull()
    {
        var consumer = A.Fake<IConsumer<string, byte[]>>();
        var seq = new Queue<KafkaConsumeResult?>([Record("m4", 29, offset: 9), null]); // > 8 bytes
        A.CallTo(() => consumer.Consume(A<TimeSpan>._)).ReturnsLazily(() => seq.Dequeue());

        var messages = await CreateReceiver(consumer, max: null).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m4"); // opt-out: oversized flows through, no ingress rejection
        A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
    }

    // ---- Push surface: KafkaTransportSubscriber.SubscribeAsync ----

    [Fact]
    public async Task Subscriber_RejectOversized_NeverHandsItToHandler()
    {
        var consumer = A.Fake<IConsumer<string, byte[]>>();
        var oversized = Record("s1", 20, offset: 5); // 20 bytes > 8
        var valid = Record("s2", 4, offset: 7);       // 4 bytes

        using var cts = new CancellationTokenSource();
        var call = 0;
        A.CallTo(() => consumer.Consume(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            call++;
            return call switch
            {
                1 => oversized,
                2 => valid,
                _ => Cancel(cts),
            };
        });

        var handled = new List<string>();
        await CreateSubscriber(consumer).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        // Oversized never deserialized/handled; only the valid message reached the handler.
        handled.ShouldBe(["s2"]);
        // Oversized poison-skipped: committed PAST it (offset 5 + 1 = 6) before the body was materialized.
        A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>.That.Matches(o => CommitsPast(o, 6))))
            .MustHaveHappened();
    }

    [Fact]
    public async Task Subscriber_NotEnforce_WhenLimitNull()
    {
        var consumer = A.Fake<IConsumer<string, byte[]>>();
        var oversized = Record("s3", 29, offset: 5); // > 8 bytes

        using var cts = new CancellationTokenSource();
        var call = 0;
        A.CallTo(() => consumer.Consume(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            call++;
            return call == 1 ? oversized : Cancel(cts);
        });

        var handled = new List<string>();
        await CreateSubscriber(consumer, max: null).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        handled.ShouldBe(["s3"]); // opt-out: oversized still handled, no ingress rejection
    }

    private static KafkaConsumeResult? Cancel(CancellationTokenSource cts)
    {
        cts.Cancel(); // stop the consume loop after the batch is settled
        return null;
    }
}
