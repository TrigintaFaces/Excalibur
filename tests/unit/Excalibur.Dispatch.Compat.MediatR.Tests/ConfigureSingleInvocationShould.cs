// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// d2kokz — author≠impl lock: <c>AddMediatRCompat</c> MUST invoke the consumer's <c>configure</c> delegate
/// EXACTLY ONCE, matching MediatR's <c>AddMediatR</c> single-invocation behavior. The pre-fix registration
/// passed <c>configure</c> straight to <c>.Configure(configure)</c>, so it was re-run when
/// <see cref="IOptions{TOptions}"/> was first materialized — a delegate with observable side effects
/// (counters, logging, one-time registration) double-fired. The fix runs <c>configure</c> once against a
/// probe and copies the probe into the DI options via <c>CopyFrom</c>.
/// </summary>
/// <remarks>
/// Non-vacuous: RED against the pre-fix <c>.Configure(configure)</c> tree (the count reads 2 after the
/// options materialize), GREEN once emission is probe-once + <c>CopyFrom</c>. SAFETY = never more than once;
/// LIVENESS = at least once AND the single run actually reached the DI options (a run whose mutations were
/// dropped would be "safe" by doing nothing). Both arms per <c>testing-patterns §3</c>.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class ConfigureSingleInvocationShould
{
    [Fact]
    public void AddMediatRCompat_InvokesConfigureExactlyOnce_IncludingAfterOptionsMaterialize()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        var invocations = 0;

        _ = services.AddMediatRCompat(cfg =>
        {
            invocations++;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;                 // an observable configuration mutation
            _ = cfg.RegisterServicesFromAssembly(typeof(IMediator).Assembly); // satisfies the >=1-assembly validator
        });

        // Runs once at registration (the probe). Not zero — the configuration must actually be applied.
        invocations.ShouldBe(1, "AddMediatRCompat must invoke `configure` once at registration time.");

        using var provider = services.BuildServiceProvider();

        // Materializing IOptions<MediatRCompatOptions> runs the registered Configure/Validate chain. It MUST
        // NOT re-run `configure` — the d2kokz bug threaded `configure` into `.Configure(configure)`, so this
        // access double-fired every side effect in the consumer's delegate.
        var options = provider.GetRequiredService<IOptions<MediatRCompatOptions>>().Value;
        invocations.ShouldBe(
            1,
            "`configure` was re-invoked when IOptions<MediatRCompatOptions> materialized: a delegate with "
            + "observable side effects (counters, logging, one-time registration) double-fires, breaking "
            + "MediatR's single-invocation contract.");

        // LIVENESS: the single invocation actually reached the DI options — the once-run was not a no-op that
        // silently dropped the consumer's configuration (a CopyFrom regression).
        options.HandlerLifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "The consumer's configuration did not reach the resolved options: `configure` ran once but its "
            + "mutations were not copied into the DI options instance (CopyFrom regression).");
    }
}
