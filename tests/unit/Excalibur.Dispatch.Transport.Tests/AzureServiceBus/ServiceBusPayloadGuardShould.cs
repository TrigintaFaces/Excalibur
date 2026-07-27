// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Author≠impl ingress payload-guard locks (bead ydvyi7) for BOTH Azure Service Bus surfaces — the pull
/// <see cref="ServiceBusTransportReceiver"/> and the push <see cref="ServiceBusTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: an over-limit body is rejected via
/// <c>PayloadSizeGuard.EnsureWithinLimit(sbMessage.Body.ToMemory().Length, ...)</c> in the convert method
/// BEFORE the body is used, and the single oversized message is dead-lettered (no requeue → cannot loop),
/// while valid messages in the same batch still flow. Non-vacuous: the differential between the null-limit
/// opt-out test (oversized passes untouched) and the enforced test (oversized dead-lettered, never handed
/// on) proves the guard is load-bearing without mutating the impl. A <see langword="null"/> limit opts out
/// (unbounded); the boundary is inclusive (N bytes OK).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ServiceBusPayloadGuardShould
{
    private const string Source = "orders-queue";
    private const int MaxPayloadBytes = 8;

    private static ServiceBusReceivedMessage SbMessage(string id, string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(body)),
            messageId: id,
            lockTokenGuid: Guid.NewGuid());

    private static ServiceBusTransportReceiver CreateReceiver(IServiceBusReceiverSeam seam, int? max = MaxPayloadBytes) =>
        new(seam, Source, NullLogger<ServiceBusTransportReceiver>.Instance, maxPayloadBytes: max);

    private static ServiceBusTransportSubscriber CreateSubscriber(IServiceBusProcessorSeam seam, int? max = MaxPayloadBytes) =>
        new(seam, Source, NullLogger<ServiceBusTransportSubscriber>.Instance, maxPayloadBytes: max);

    // Fake ProcessMessageEventArgs (a data-shaped …Args type, FakeItEasy-safe per ADR-142 §D7): the
    // virtual settle methods (DeadLetterMessageAsync/CompleteMessageAsync) are intercepted so no real SDK
    // settlement runs — letting us assert how an oversized push message is surfaced. The concrete
    // ServiceBusReceiver the ctor requires must NOT be faked (§D7 forbids faking concrete Azure.* SDK
    // classes); a REAL, unconnected receiver is used instead — no network I/O happens because the args'
    // settle methods are intercepted, so the receiver is never actually invoked.
    private static ProcessMessageEventArgs FakeArgs(ServiceBusReceivedMessage message)
    {
        var receiver = RealUnconnectedReceiver();
        return A.Fake<ProcessMessageEventArgs>(o => o.WithArgumentsForConstructor(
            () => new ProcessMessageEventArgs(message, receiver, CancellationToken.None)));
    }

    // A real, unconnected ServiceBusReceiver — no network I/O occurs until a real operation is invoked, and
    // the faked ProcessMessageEventArgs intercepts every settle call so the receiver is never used.
    private static ServiceBusReceiver RealUnconnectedReceiver() =>
        new ServiceBusClient(
            "Endpoint=sb://unit.test.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey="
            + Convert.ToBase64String(new byte[16]))
            .CreateReceiver("q");

    // ---- Pull surface: ServiceBusTransportReceiver.ReceiveAsync ----

    [Fact]
    public async Task Receiver_RejectOversized_ReturnValidInSameBatch()
    {
        var seam = A.Fake<IServiceBusReceiverSeam>();
        var oversized = SbMessage("m1", "0123456789ABCDEFGHIJ"); // 20 bytes > 8
        var valid = SbMessage("m2", "ok!!");                     // 4 bytes
        A.CallTo(() => seam.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
            .Returns(new[] { oversized, valid } as IReadOnlyList<ServiceBusReceivedMessage>);

        var messages = await CreateReceiver(seam).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m2");
        // Oversized dead-lettered (never returned, never materialized past the guard).
        A.CallTo(() => seam.DeadLetterMessageAsync(
            A<ServiceBusReceivedMessage>.That.Matches(m => m.MessageId == "m1"),
            "PayloadTooLarge", A<string?>._, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task Receiver_AcceptMessage_AtExactLimit()
    {
        var seam = A.Fake<IServiceBusReceiverSeam>();
        A.CallTo(() => seam.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
            .Returns(new[] { SbMessage("m3", "12345678") } as IReadOnlyList<ServiceBusReceivedMessage>); // exactly 8

        var messages = await CreateReceiver(seam).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m3");
        A.CallTo(() => seam.DeadLetterMessageAsync(
            A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Receiver_NotEnforce_WhenLimitNull()
    {
        var seam = A.Fake<IServiceBusReceiverSeam>();
        A.CallTo(() => seam.ReceiveMessagesAsync(A<int>._, A<CancellationToken>._))
            .Returns(new[] { SbMessage("m4", "way-too-large-for-eight-bytes") } as IReadOnlyList<ServiceBusReceivedMessage>);

        var messages = await CreateReceiver(seam, max: null).ReceiveAsync(10, CancellationToken.None);

        messages.Count.ShouldBe(1);
        messages[0].Id.ShouldBe("m4");
        A.CallTo(() => seam.DeadLetterMessageAsync(
            A<ServiceBusReceivedMessage>._, A<string?>._, A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    // ---- Push surface: ServiceBusTransportSubscriber.SubscribeAsync ----

    [Fact]
    public async Task Subscriber_RejectOversized_NeverHandsItToHandler()
    {
        var processor = A.Fake<IServiceBusProcessorSeam>();
        var oversizedArgs = FakeArgs(SbMessage("s1", "0123456789ABCDEFGHIJ")); // 20 bytes > 8
        var validArgs = FakeArgs(SbMessage("s2", "ok!!"));                      // 4 bytes

        using var cts = new CancellationTokenSource();
        var handled = new List<string>();
        var subscribeTask = CreateSubscriber(processor).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        await WaitForHandlerRegistered(processor);

        // Push delivers one message per handler invocation. Handlers run to completion synchronously
        // (every awaited settle/handler task is already completed), so no polling is needed after raise.
        processor.ProcessMessageAsync += Raise.FreeForm<Func<ProcessMessageEventArgs, Task>>.With(oversizedArgs);
        processor.ProcessMessageAsync += Raise.FreeForm<Func<ProcessMessageEventArgs, Task>>.With(validArgs);

        // Oversized never deserialized/handled; only the valid message reached the handler.
        handled.ShouldBe(["s2"]);
        // Oversized dead-lettered (no requeue) before the body was used.
        A.CallTo(() => oversizedArgs.DeadLetterMessageAsync(
            oversizedArgs.Message, "PayloadTooLarge", A<string?>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => validArgs.DeadLetterMessageAsync(
            A<ServiceBusReceivedMessage>._, "PayloadTooLarge", A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();

        await cts.CancelAsync();
        await subscribeTask;
    }

    [Fact]
    public async Task Subscriber_NotEnforce_WhenLimitNull()
    {
        var processor = A.Fake<IServiceBusProcessorSeam>();
        var oversizedArgs = FakeArgs(SbMessage("s3", "way-too-large-for-eight-bytes"));

        using var cts = new CancellationTokenSource();
        var handled = new List<string>();
        var subscribeTask = CreateSubscriber(processor, max: null).SubscribeAsync(
            (msg, _) => { handled.Add(msg.Id); return Task.FromResult(MessageAction.Acknowledge); },
            cts.Token);

        await WaitForHandlerRegistered(processor);

        processor.ProcessMessageAsync += Raise.FreeForm<Func<ProcessMessageEventArgs, Task>>.With(oversizedArgs);

        handled.ShouldBe(["s3"]); // opt-out: oversized still handled, no ingress rejection
        A.CallTo(() => oversizedArgs.DeadLetterMessageAsync(
            A<ServiceBusReceivedMessage>._, "PayloadTooLarge", A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();

        await cts.CancelAsync();
        await subscribeTask;
    }

    private static async Task WaitForHandlerRegistered(IServiceBusProcessorSeam processor)
    {
        var started = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () =>
            {
                try
                {
                    A.CallTo(() => processor.StartProcessingAsync(A<CancellationToken>._)).MustHaveHappened();
                    return true;
                }
                catch
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        started.ShouldBeTrue("subscriber should register its handler and start the processor");
    }
}
