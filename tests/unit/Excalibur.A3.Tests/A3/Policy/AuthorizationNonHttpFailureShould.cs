// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Regression lock for Excalibur.Dispatch-273d1m: the Cedar and OPA evaluators previously caught only
/// <c>TaskCanceledException</c> (non-caller-cancelled) and <c>HttpRequestException</c>, so a policy engine
/// that is reachable but throws something else -- a parser failure on a malformed response, an
/// unparseable endpoint configuration -- bypassed the FailClosed/FailOpen policy entirely and propagated
/// into the caller. Both evaluators now route every non-caller-cancellation failure through
/// <c>FailureDecision</c>, the single decision point.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationNonHttpFailureShould
{
    private static readonly AuthorizationSubject Subject = new("user-42", "tenant-1", null);
    private static readonly AuthorizationAction Action = new("Read", null);
    private static readonly AuthorizationResource Resource = new("Order", "order-123", null);

    // Cedar EventIds: 3205 fail-open permit audit, 3206 unexpected-failure warning.
    [Fact]
    public async Task Cedar_FailClosed_DeniesOnANonHttpEngineFailure_InsteadOfThrowing()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseCedarPolicy(options =>
            {
                options.Endpoint = "http://cedar-test:8080";
                options.FailClosed = true;
            }),
            new ThrowingHandler(() => new InvalidOperationException("simulated malformed Cedar response")));

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);

        decision.Effect.ShouldBe(AuthorizationEffect.Deny, "a non-HTTP engine failure must deny under FailClosed=true, not throw");
    }

    [Fact]
    public async Task Cedar_FailOpen_PermitsOnANonHttpEngineFailure_AndLogsLoudly()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseCedarPolicy(options =>
            {
                options.Endpoint = "http://cedar-test:8080";
                options.FailClosed = false;
            }),
            new ThrowingHandler(() => new InvalidOperationException("simulated malformed Cedar response")));
        var collector = provider.GetFakeLogCollector();

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);

        decision.Effect.ShouldBe(AuthorizationEffect.Permit);
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3206,
            "the unexpected-failure path must be logged loudly (EventId 3206), naming the real exception");
    }

    [Fact]
    public async Task Cedar_DoesNotSwallowCallerCancellation()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseCedarPolicy(options =>
            {
                options.Endpoint = "http://cedar-test:8080";
                options.FailClosed = true;
            }),
            new ThrowingHandler(() => new InvalidOperationException("should never run - caller already cancelled")));

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Cooperative cancellation must propagate, never be swallowed into a Deny/Permit decision.
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(Subject, Action, Resource, cts.Token));
    }

    // OPA EventIds: 3105 fail-open permit audit, 3106 unexpected-failure warning.
    [Fact]
    public async Task Opa_FailClosed_DeniesOnANonHttpEngineFailure_InsteadOfThrowing()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseOpaPolicy(options =>
            {
                options.Endpoint = "http://opa-test:8181";
                options.PolicyPath = "v1/data/authz/allow";
                options.FailClosed = true;
            }),
            new ThrowingHandler(() => new InvalidOperationException("simulated malformed OPA response")));

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);

        decision.Effect.ShouldBe(AuthorizationEffect.Deny, "a non-HTTP engine failure must deny under FailClosed=true, not throw");
    }

    [Fact]
    public async Task Opa_FailOpen_PermitsOnANonHttpEngineFailure_AndLogsLoudly()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseOpaPolicy(options =>
            {
                options.Endpoint = "http://opa-test:8181";
                options.PolicyPath = "v1/data/authz/allow";
                options.FailClosed = false;
            }),
            new ThrowingHandler(() => new InvalidOperationException("simulated malformed OPA response")));
        var collector = provider.GetFakeLogCollector();

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);

        decision.Effect.ShouldBe(AuthorizationEffect.Permit);
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3106,
            "the unexpected-failure path must be logged loudly (EventId 3106), naming the real exception");
    }

    [Fact]
    public async Task Opa_DoesNotSwallowCallerCancellation()
    {
        using var provider = BuildProvider(
            services => services.AddExcaliburA3().UseOpaPolicy(options =>
            {
                options.Endpoint = "http://opa-test:8181";
                options.PolicyPath = "v1/data/authz/allow";
                options.FailClosed = true;
            }),
            new ThrowingHandler(() => new InvalidOperationException("should never run - caller already cancelled")));

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(Subject, Action, Resource, cts.Token));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configurePolicy, HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddFakeLogging());
        configurePolicy(services);

        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        return services.BuildServiceProvider();
    }

    /// <summary>An HTTP handler that throws an arbitrary non-HTTP, non-cancellation exception, standing in
    /// for a reachable-but-misbehaving engine (a parser failure, a bad endpoint configuration) rather than
    /// an outage.</summary>
    private sealed class ThrowingHandler(Func<Exception> exceptionFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exceptionFactory();
        }
    }
}
