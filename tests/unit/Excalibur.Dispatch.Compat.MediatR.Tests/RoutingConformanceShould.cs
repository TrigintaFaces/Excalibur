// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-95tyq1 — author≠impl lock that the compat IMediator routes through the canonical
/// IDispatcher pipeline (SA-ruled Option-A bridge, msg 17838): a canonical IDispatchMiddleware
/// registered via AddDispatch(d => d.UseMiddleware&lt;&gt;()) MUST wrap the compat Send — proving
/// the migrated consumer genuinely runs on Dispatch (canonical middleware/observability/resilience),
/// not a bypass. Non-vacuous: RED until WS1 routing wraps Send → DispatchAsync (facade throws now).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class RoutingConformanceShould
{
    /// <summary>Shared sink so the test can observe that the canonical middleware ran.</summary>
    private sealed class RoutingProbe
    {
        public bool Wrapped { get; set; }
    }

    /// <summary>Canonical-pipeline middleware that records it wrapped the dispatch, then continues.</summary>
    private sealed class RoutingProbeMiddleware(RoutingProbe probe) : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

        public ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message,
            IMessageContext context,
            DispatchRequestDelegate nextDelegate,
            CancellationToken cancellationToken)
        {
            probe.Wrapped = true;
            return nextDelegate(message, context, cancellationToken);
        }
    }

    [Fact] // 95tyq1: canonical middleware wraps the compat Send (proves routing through IDispatcher).
    public async Task RouteCompatSendThroughTheCanonicalPipeline()
    {
        var probe = new RoutingProbe();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(probe);
        // Configure the canonical pipeline FIRST so the compat layer's self-registered AddDispatch
        // does not overwrite the middleware invoker.
        services.AddDispatch(dispatch => dispatch.UseMiddleware<RoutingProbeMiddleware>());
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(PingHandler).Assembly));

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new Ping());

        result.ShouldBe("pong");
        probe.Wrapped.ShouldBeTrue("the canonical IDispatchMiddleware must wrap the compat Send");
    }
}
