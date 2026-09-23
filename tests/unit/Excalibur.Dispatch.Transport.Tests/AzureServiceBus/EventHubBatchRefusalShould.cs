// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Binds what happens when the Event Hubs batch refuses the message: it is reported, never dropped.
/// </summary>
/// <remarks>
/// <para>
/// <b>The path under test is the DEFAULT one.</b> The bus routes to its CloudEvents publish only when a
/// bridge and an encoder are both supplied; otherwise it takes the plain path exercised here. So this is
/// the behaviour a consumer gets when they have not opted into CloudEvents.
/// </para>
/// <para>
/// <b>Why the refusal is forced through the SDK factory's callback.</b> The batch's size accounting is the
/// SDK's, and a test that tries to provoke a refusal by choosing a payload size binds itself to that
/// accounting. <c>tryAddCallback</c> states the condition directly: this batch refuses. What the bus does
/// about it is the thing under test.
/// </para>
/// <para>
/// <b>Why an existing arm could not catch this.</b> The sibling suite already publishes through this same
/// path and asserts the event reaches the wire — with a one-megabyte batch, which never refuses. Its
/// assertion is correct and its fixture makes the branch unreachable, so no change to the bus could turn
/// it red. Fixture SIZE, not fixture shape, was the blind spot.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class EventHubBatchRefusalShould
{
    /// <summary>
    /// SAFETY. A refused message surfaces as a failure rather than a successful publish that sent nothing.
    /// </summary>
    /// <remarks>
    /// Both halves are asserted. Throwing is what tells the caller; the empty wire is what makes the old
    /// behaviour data loss rather than a slow path — the publish previously returned successfully having
    /// transmitted an empty batch, so the message was gone and the caller had no way to know.
    /// </remarks>
    [Fact]
    public async Task Refuse_to_report_success_when_the_batch_rejects_the_message()
    {
        var reachedTheWire = new List<EventData>();
        var producer = ProducerWithBatch(reachedTheWire, accepts: false);

        await using var bus = Bus(producer);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => bus.PublishAsync(new RefusedEvent(), Context(), CancellationToken.None));

        reachedTheWire.ShouldBeEmpty(
            "nothing was transmitted, which is precisely why a silent success here loses the message");
    }

    /// <summary>
    /// LIVENESS. A message the batch accepts is still published.
    /// </summary>
    /// <remarks>
    /// The pair to the arm above, and not a formality: a bus that threw on every publish would satisfy the
    /// safety arm completely while transmitting nothing at all.
    /// </remarks>
    [Fact]
    public async Task Publish_a_message_the_batch_accepts()
    {
        var reachedTheWire = new List<EventData>();
        var producer = ProducerWithBatch(reachedTheWire, accepts: true);

        await using var bus = Bus(producer);

        await bus.PublishAsync(new RefusedEvent(), Context(), CancellationToken.None);

        _ = reachedTheWire.ShouldHaveSingleItem();
    }

    private static IEventHubProducer ProducerWithBatch(List<EventData> store, bool accepts)
    {
        var batch = EventHubsModelFactory.EventDataBatch(
            batchSizeBytes: 1024,
            batchEventStore: store,
            batchOptions: null,
            tryAddCallback: _ => accepts);

        var producer = A.Fake<IEventHubProducer>();
#pragma warning disable CA2012 // ValueTask stored by the fake rather than awaited here
        _ = A.CallTo(() => producer.CreateBatchAsync(A<CancellationToken>._)).Returns(batch);
#pragma warning restore CA2012
        _ = A.CallTo(() => producer.SendAsync(A<EventDataBatch>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        return producer;
    }

    /// <summary>
    /// Builds the bus on its DEFAULT path: no bridge and no encoder, which is what routes to the plain
    /// publish rather than the CloudEvents one.
    /// </summary>
    private static AzureEventHubMessageBus Bus(IEventHubProducer producer) =>
        new(producer, A.Fake<IPayloadSerializer>(), NullLogger<AzureEventHubMessageBus>.Instance);

    private static IMessageContext Context()
    {
        var context = A.Fake<IMessageContext>();
        _ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
        _ = A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
        return context;
    }

    private sealed class RefusedEvent : IDispatchEvent
    {
    }
}
