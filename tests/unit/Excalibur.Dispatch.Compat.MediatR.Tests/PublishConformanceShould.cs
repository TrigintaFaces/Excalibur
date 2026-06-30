// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-dcnmhh — author≠impl conformance lock for AC-3 (Publish invokes ALL notification
/// handlers) + EC-3 (zero registered handlers is a no-op, not an error). Non-vacuous:
/// RED until WS1 Publish adapter (g1jewr) fans out through the canonical event path; GREEN
/// once both registered compat INotificationHandler&lt;T&gt; adapters are invoked.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class PublishConformanceShould
{
    [Fact] // AC-3: every registered INotificationHandler<Pinged> is invoked.
    public async Task InvokeAllRegisteredHandlers_WhenNotificationIsPublished()
    {
        var sink = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddSingleton(sink);
        services.AddSingleton<INotificationHandler<Pinged>>(new RecordingNotificationHandler("h1", sink));
        services.AddSingleton<INotificationHandler<Pinged>>(new RecordingNotificationHandler("h2", sink));
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(Pinged).Assembly));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.Publish(new Pinged());

        sink.ShouldBe(["h1", "h2"], ignoreOrder: true);
    }

    [Fact] // EC-3: publishing with zero registered handlers is a no-op (matches incumbent).
    public async Task BeNoOp_WhenNoHandlersRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(Unheard).Assembly));
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        // Unheard has NO handler in the assembly -> Publish must complete without throwing.
        await Should.NotThrowAsync(async () => await mediator.Publish(new Unheard()));
    }

    [Fact] // AC-3 via the generic IPublisher.Publish<TNotification> overload (C3 fidelity).
    public async Task InvokeHandlers_WhenGenericPublishOverloadIsCalled()
    {
        var sink = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddSingleton(sink);
        services.AddSingleton<INotificationHandler<Pinged>>(new RecordingNotificationHandler("h1", sink));
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(Pinged).Assembly));
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.Publish<Pinged>(new Pinged());

        sink.ShouldBe(["h1"]);
    }

    [Fact] // EC-6: a pre-cancelled token surfaces OperationCanceledException through Publish.
    public async Task SurfaceOperationCanceled_WhenTokenAlreadyCancelled()
    {
        var sink = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddSingleton(sink);
        services.AddSingleton<INotificationHandler<Pinged>>(new RecordingNotificationHandler("h1", sink));
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(Pinged).Assembly));
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await mediator.Publish(new Pinged(), cts.Token));
    }
}
