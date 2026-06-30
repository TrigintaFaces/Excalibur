// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-dazry5 — author≠impl conformance lock for AC-1 (namespace-swap compiles) + AC-2
/// (Send returns the unwrapped response). Also covers AC-7 (handler-not-found) and EC-1
/// (void/Unit request). Non-vacuous: RED until WS1 Send adapter (f4zy8k/g1r81z/q00wv8)
/// + AddMediatRCompat registration (h4pqru) land; GREEN once they route through IDispatcher.
/// Binds the SA-ruled bridge (msg 17838): facade wraps -> IDispatcher.DispatchAsync -> unwraps
/// IMessageResult&lt;TResponse&gt;.Value.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class SendConformanceShould
{
    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();

        // Canonical Dispatch core (provides IDispatcher) — the realistic consumer scenario.
        // NOTE: if AddMediatRCompat is meant to be the sole swap-only call, it should self-register
        // Dispatch core; flagged to Backend. AddDispatch is idempotent so this stays correct either way.
        services.AddLogging();
        services.AddDispatch();

        // Registration API is spec-pinned (§API Contracts): AddMediatRCompat + Action<MediatRCompatOptions>.
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(PingHandler).Assembly));

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact] // AC-1: a handler authored under the compat namespace compiles against the package.
    public void CompileAgainstCompatSurface_WhenOnlyTheNamespaceIsSwapped()
    {
        // The mere existence + assignability of these fixtures (declared in the compat namespace,
        // implementing IRequest/IRequestHandler) is the AC-1 swap-compiles proof.
        IRequest<string> request = new Ping();
        IRequestHandler<Ping, string> handler = new PingHandler();

        request.ShouldNotBeNull();
        handler.ShouldNotBeNull();
    }

    [Fact] // AC-2: Send routes through the dispatcher and returns the unwrapped TResponse.
    public async Task ReturnUnwrappedResponse_WhenSendIsCalled()
    {
        var mediator = BuildMediator();

        var result = await mediator.Send(new Ping());

        result.ShouldBe("pong");
    }

    [Fact] // AC-2 via ISender non-generic overload (C3 fidelity).
    public async Task ReturnUnwrappedResponse_WhenSendObjectOverloadIsCalled()
    {
        var mediator = BuildMediator();

        var result = await mediator.Send((object)new Ping());

        result.ShouldBe("pong");
    }

    [Fact] // EC-1: a void (Unit) request completes without surfacing a real return value.
    public async Task CompleteVoidRequest_WhenHandlerReturnsUnit()
    {
        var mediator = BuildMediator();

        var result = await mediator.Send(new VoidPing());

        result.ShouldBe(Unit.Value);
    }

    [Fact] // AC-7: Send with no registered handler throws an incumbent-equivalent "not found" shape.
    public async Task Throw_WhenNoHandlerRegisteredForRequest()
    {
        var mediator = BuildMediator();

        // p2m3yy committed HandlerNotFoundException : InvalidOperationException (incumbent-equivalent).
        await Should.ThrowAsync<HandlerNotFoundException>(async () => await mediator.Send(new Orphan()));
    }

    [Fact] // EC-6: a pre-cancelled token surfaces OperationCanceledException through Send.
    public async Task SurfaceOperationCanceled_WhenTokenAlreadyCancelled()
    {
        var mediator = BuildMediator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await mediator.Send(new Ping(), cts.Token));
    }
}
