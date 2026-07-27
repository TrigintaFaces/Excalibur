// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Observability.Tests.Outbox;

/// <summary>
/// Regression locks for <see cref="MessageBusOutboxPublisher"/>'s deferred-publish trace restore.
/// </summary>
/// <remarks>
/// A store-and-forward outbox message must publish with the <strong>enqueue-time</strong> W3C
/// <c>traceparent</c> persisted on its headers — NOT the flusher's ambient <see cref="Activity.Current"/>
/// at flush time — so the originating distributed trace is preserved across the store hop.
/// These are author≠impl locks bound to committed impl <c>c2a5420be</c> (bd-fa303y).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class W3COutboxTraceRestoreShould
{
    // Enqueue-time traceparent persisted on the staged message (distinct trace-id from any flush-time activity).
    private const string EnqueueTimeTraceParent =
        "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    /// <summary>
    /// Flushes a single staged message through a fake message bus and returns the context the bus received.
    /// </summary>
    private static async Task<IMessageContext> FlushAndCaptureContextAsync(OutboundMessage staged)
    {
        var outboxStore = A.Fake<IOutboxStore>();
        A.CallTo(() => outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => new ValueTask<IEnumerable<OutboundMessage>>(new[] { staged }));
        // MarkSentAsync returns a non-generic ValueTask — a faked call completes by default (no setup needed).

        var serializer = A.Fake<IPayloadSerializer>();
        var serviceProvider = A.Fake<IServiceProvider>();
        var logger = A.Fake<ILogger<MessageBusOutboxPublisher>>();

        IMessageContext? capturedContext = null;
        var messageBus = A.Fake<IMessageBusAdapter>();
        A.CallTo(() => messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Invokes((IDispatchMessage _, IMessageContext ctx, CancellationToken _) => capturedContext = ctx)
            .Returns(Task.FromResult(A.Fake<IMessageResult>()));

        var publisher = new MessageBusOutboxPublisher(outboxStore, serializer, messageBus, serviceProvider, logger);

        // Establish a flush-time ambient Activity with a DIFFERENT trace-id than the enqueue-time value,
        // so a regression to Activity.Current (instead of the stored header) is observable.
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("W3COutboxTraceRestoreShould");
        using var flushActivity = source.StartActivity("flush");

        flushActivity.ShouldNotBeNull("test harness must establish a flush-time ambient activity");
        flushActivity!.Id.ShouldNotBe(EnqueueTimeTraceParent, "flush activity must differ from the enqueue-time trace");

        await publisher.PublishPendingMessagesAsync(CancellationToken.None);

        capturedContext.ShouldNotBeNull("message bus must have received the rebuilt publish context");
        return capturedContext!;
    }

    [Fact]
    public async Task RestoreEnqueueTimeTraceParent_NotFlusherActivityCurrent()
    {
        // Arrange — staged message carries the enqueue-time traceparent on its headers.
        var staged = new OutboundMessage(
            messageType: "TestMessage",
            payload: [1, 2, 3],
            destination: "test-destination",
            headers: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["traceparent"] = EnqueueTimeTraceParent,
            });

        // Act
        var context = await FlushAndCaptureContextAsync(staged);

        // Assert — the published context carries the ENQUEUE-TIME trace, not the flusher's Activity.Current.
        context.GetTraceParent().ShouldBe(EnqueueTimeTraceParent);
        context.GetTraceParent().ShouldNotBe(Activity.Current?.Id);
    }

    [Fact]
    public async Task RestoreEnqueueTimeTraceState_SymmetricWithTraceParent()
    {
        // S862 REVIEW P1 (0a7kvu): tracestate is persisted symmetric with traceparent and restored onto the
        // rebuilt publish context (the item slot the consumer's W3CTraceContextMiddleware reads), so
        // multi-vendor trace state survives the store-and-forward hop. RED if the tracestate persist/restore
        // is dropped (only traceparent restored).
        const string TraceState = "rojo=00f067aa0ba902b7,congo=t61rcWkgMzE";
        var staged = new OutboundMessage(
            messageType: "TestMessage",
            payload: [1, 2, 3],
            destination: "test-destination",
            headers: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["traceparent"] = EnqueueTimeTraceParent,
                ["tracestate"] = TraceState,
            });

        // Act
        var context = await FlushAndCaptureContextAsync(staged);

        // Assert — both traceparent AND tracestate restored (symmetric round-trip).
        context.GetTraceParent().ShouldBe(EnqueueTimeTraceParent);
        context.Items.TryGetValue("tracestate", out var restored).ShouldBeTrue("tracestate must be restored symmetric with traceparent (P1)");
        (restored as string).ShouldBe(TraceState);
    }

    [Fact]
    public async Task SkipRestore_WhenNoStagedTraceParent_FailOpenStillPublishes()
    {
        // Arrange — no traceparent header staged.
        var staged = new OutboundMessage(
            messageType: "TestMessage",
            payload: [1, 2, 3],
            destination: "test-destination");

        // Act
        var context = await FlushAndCaptureContextAsync(staged);

        // Assert — nothing restored (does NOT fall back to Activity.Current), send unaffected.
        context.GetTraceParent().ShouldBeNull();
    }

    [Fact]
    public async Task SkipRestore_WhenStagedTraceParentMalformed_FailOpenStillPublishes()
    {
        // Arrange — malformed stored value; the BCL parse rejects it and restore is skipped (best-effort).
        var staged = new OutboundMessage(
            messageType: "TestMessage",
            payload: [1, 2, 3],
            destination: "test-destination",
            headers: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["traceparent"] = "not-a-valid-traceparent",
            });

        // Act
        var context = await FlushAndCaptureContextAsync(staged);

        // Assert — malformed value is not propagated; publish still occurs (fail-open).
        context.GetTraceParent().ShouldBeNull();
    }
}
