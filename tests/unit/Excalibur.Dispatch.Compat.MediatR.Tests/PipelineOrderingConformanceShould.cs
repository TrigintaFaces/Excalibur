// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-nu2y9r — author≠impl conformance lock for AC-5 (pipeline behavior nesting order),
/// EC-4 (short-circuit skips the handler) and EC-5 (throw propagates un-swallowed).
/// Binds the SA-ruled bridge (msg 17838): the compat IPipelineBehavior chain runs
/// MediatR-ordered, nested inside the generated adapter handler, hosted under IDispatcher.
/// Deterministic order assertion; non-vacuous (RED until npnsl8 wires the nested chain).
///
/// Behavior registration order is expressed via cfg.AddOpenBehavior(typeof(T&lt;,&gt;)) in
/// declaration order (the landed MediatRCompatOptions API; registration order = pipeline
/// order, MediatR-faithful).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class PipelineOrderingConformanceShould
{
    [Fact] // AC-5: A→B→C→handler→C→B→A (registration order, nested).
    public async Task ExecuteBehaviorsInNestedRegistrationOrder_AroundTheHandler()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddSingleton(order);
        services.AddMediatRCompat(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(OrderedPingHandler).Assembly);
            cfg.AddOpenBehavior(typeof(BehaviorA<,>));
            cfg.AddOpenBehavior(typeof(BehaviorB<,>));
            cfg.AddOpenBehavior(typeof(BehaviorC<,>));
        });
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.Send(new OrderedPing());

        response.ShouldBe("ordered-pong");
        order.ShouldBe([
            "A-before", "B-before", "C-before",
            "handler",
            "C-after", "B-after", "A-after",
        ]);
    }

    [Fact] // EC-4: a behavior that does not call next short-circuits — handler MUST NOT run.
    public async Task NotInvokeHandler_WhenBehaviorShortCircuits()
    {
        var sink = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddSingleton(sink);
        services.AddMediatRCompat(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ShortCircuitPingHandler).Assembly);
            cfg.AddOpenBehavior(typeof(ShortCircuitBehavior<,>));
        });
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.Send(new ShortCircuitPing());

        // The behavior returns default(string) (null) without calling next:
        response.ShouldBeNull();
        sink.ShouldContain("short-circuited"); // the short-circuiting behavior ran
        sink.ShouldNotContain("handler-ran");  // the downstream handler did NOT run (EC-4)
    }

    [Fact] // EC-5: an exception thrown in a behavior propagates un-swallowed, type preserved.
    public async Task PropagateException_WhenBehaviorThrows()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddMediatRCompat(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ThrowPingHandler).Assembly);
            cfg.AddOpenBehavior(typeof(ThrowingBehavior<,>));
        });
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<PipelineBoomException>(
            async () => await mediator.Send(new ThrowPing()));
        ex.Message.ShouldBe("behavior boom");
    }
}
