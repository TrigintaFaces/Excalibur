// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MassTransit.Tests;

/// <summary>
/// bd-i5hrxo — author≠impl lock for the WS3 MassTransit consumer shim (0zcbb7, AC-11):
/// AddMassTransitConsumer registers the consumer + adapter; the adapter bridges the canonical
/// IEventHandler&lt;TMessage&gt; entry point to the MassTransit IConsumer&lt;TMessage&gt;.Consume,
/// passing a ConsumeContext whose Message IS the dispatched event. Regression lock on the
/// committed shim (non-vacuous: asserts the consumer ran AND received the exact event instance).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class ConsumerShimShould
{
    private sealed class OrderPlaced : IDispatchEvent
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class RecordingOrderConsumer : IConsumer<OrderPlaced>
    {
        public ConsumeContext<OrderPlaced>? Received { get; private set; }

        public Task Consume(ConsumeContext<OrderPlaced> context)
        {
            Received = context;
            return Task.CompletedTask;
        }
    }

    [Fact] // AC-11: dispatching TMessage invokes the consumer's Consume with context.Message == event.
    public async Task DispatchToConsumer_WhenEventHandlerAdapterIsInvoked()
    {
        // Pre-register the consumer instance so AddMassTransitConsumer's TryAddScoped keeps ours
        // (lets us inspect what the shim delivered).
        var consumer = new RecordingOrderConsumer();
        var services = new ServiceCollection();
        services.AddSingleton(consumer);
        services.AddMassTransitConsumer<RecordingOrderConsumer, OrderPlaced>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<OrderPlaced>>();

        var evt = new OrderPlaced { Id = "order-1" };
        await handler.HandleAsync(evt, CancellationToken.None);

        consumer.Received.ShouldNotBeNull();
        consumer.Received!.Message.ShouldBeSameAs(evt);
    }

    [Fact] // The adapter forwards the cancellation token into the ConsumeContext.
    public async Task ForwardCancellationToken_IntoConsumeContext()
    {
        var consumer = new RecordingOrderConsumer();
        var services = new ServiceCollection();
        services.AddSingleton(consumer);
        services.AddMassTransitConsumer<RecordingOrderConsumer, OrderPlaced>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<OrderPlaced>>();
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(new OrderPlaced { Id = "order-2" }, cts.Token);

        consumer.Received.ShouldNotBeNull();
        consumer.Received!.CancellationToken.ShouldBe(cts.Token);
    }
}
