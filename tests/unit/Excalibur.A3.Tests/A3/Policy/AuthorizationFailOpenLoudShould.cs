// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Regression lock for the A3 authorization fail-open-LOUD hardening (bead <c>394k5n</c>). When an
/// authorization backend is configured <c>FailClosed=false</c>, a fail-open decision MUST be loud, not
/// silent: (a) a startup <strong>warning</strong> fires when the evaluator is constructed, and (b) on a
/// backend outage the evaluator returns <c>Permit</c> AND emits a <strong>warning-level audit log</strong>
/// naming the actor/action/resource. Asserted against the real evaluators (OPA + Cedar) via a captured
/// logger, with a throwing HTTP handler standing in for the outage. RED on the pre-fix silent-Permit.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationFailOpenLoudShould
{
    private static readonly AuthorizationSubject Subject = new("user-42", "tenant-1", null);
    private static readonly AuthorizationAction Action = new("Read", null);
    private static readonly AuthorizationResource Resource = new("Order", "order-123", null);

    // OPA fail-open EventIds (OpaAuthorizationEvaluator): 3104 startup warning, 3105 fail-open permit audit.
    [Fact]
    public async Task Opa_FailOpen_FiresStartupWarning_AndLoudPermitAudit_OnOutage()
    {
        using var provider = BuildProvider(services => services
            .AddExcaliburA3()
            .UseOpaPolicy(options =>
            {
                options.Endpoint = "http://opa-test:8181";
                options.PolicyPath = "v1/data/authz/allow";
                options.FailClosed = false; // arm fail-open
            }));
        var collector = provider.GetFakeLogCollector();

        // (a) constructing the evaluator emits the fail-open-configured startup warning.
        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3104,
            "fail-open configuration must emit a loud startup warning (EventId 3104)");

        // (b) on a backend outage the decision is Permit AND a loud audit warning is emitted.
        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);
        decision.Effect.ShouldBe(AuthorizationEffect.Permit);
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3105,
            "a fail-open PERMIT must be logged loudly (EventId 3105), never silent");
    }

    // Cedar fail-open EventIds (CedarAuthorizationEvaluator): 3204 startup warning, 3205 fail-open permit audit.
    [Fact]
    public async Task Cedar_FailOpen_FiresStartupWarning_AndLoudPermitAudit_OnOutage()
    {
        using var provider = BuildProvider(services => services
            .AddExcaliburA3()
            .UseCedarPolicy(options =>
            {
                options.Endpoint = "http://cedar-test:8080";
                options.FailClosed = false; // arm fail-open
            }));
        var collector = provider.GetFakeLogCollector();

        var evaluator = provider.GetRequiredService<IAuthorizationEvaluator>();
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3204,
            "fail-open configuration must emit a loud startup warning (EventId 3204)");

        var decision = await evaluator.EvaluateAsync(Subject, Action, Resource, CancellationToken.None);
        decision.Effect.ShouldBe(AuthorizationEffect.Permit);
        collector.GetSnapshot().ShouldContain(
            entry => entry.Level == LogLevel.Warning && entry.Id.Id == 3205,
            "a fail-open PERMIT must be logged loudly (EventId 3205), never silent");
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configurePolicy)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddFakeLogging());
        configurePolicy(services);

        // Every policy HTTP call throws -> the outage/fail-open path is exercised deterministically.
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => new OutageHandler()));

        return services.BuildServiceProvider();
    }

    private sealed class OutageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated authorization backend outage");
    }
}
