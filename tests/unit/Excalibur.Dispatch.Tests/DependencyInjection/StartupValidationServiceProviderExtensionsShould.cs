// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Non-vacuous safety+liveness lock for <see cref="StartupValidationServiceProviderExtensions.ValidateStartupGates"/>
/// (7qooby): the host-less trigger for <c>ValidateOnStart()</c> gates. A consumer who builds an
/// <see cref="IServiceProvider"/> and never starts a host (custom serverless runtime, manual provider) must be able
/// to fire the fail-fast gates on demand — otherwise they are silently inert on that topology.
/// </summary>
/// <remarks>
/// Uses a minimal self-contained <c>ValidateOnStart()</c> gate (an options type + a failing
/// <see cref="IValidateOptions{TOptions}"/>) so the lock binds the extension's contract — "fire the framework's own
/// <see cref="IStartupValidator"/> host-less" — rather than any one production gate. Both arms per testing-patterns
/// §3: SAFETY — a failing gate throws when triggered; LIVENESS — a valid config passes clean and an empty container
/// no-ops. The inertness arm proves the extension is load-bearing (RED if it were a no-op).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StartupValidationServiceProviderExtensionsShould
{
    private sealed class GateOptions
    {
        public bool AllowVolatile { get; set; }
    }

    /// <summary>Mirrors the durability gates: fails unless the host explicitly accepts the volatile store.</summary>
    private sealed class GateValidator : IValidateOptions<GateOptions>
    {
        public ValidateOptionsResult Validate(string? name, GateOptions options) =>
            options.AllowVolatile
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("volatile store configured without an explicit opt-in.");
    }

    private static ServiceProvider BuildProvider(bool allowVolatile, bool registerGate = true)
    {
        var services = new ServiceCollection();
        if (registerGate)
        {
            services.AddSingleton<IValidateOptions<GateOptions>, GateValidator>();
            _ = services.AddOptions<GateOptions>()
                .Configure(o => o.AllowVolatile = allowVolatile)
                .ValidateOnStart();
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Fire_a_failing_gate_host_less_that_was_otherwise_inert()
    {
        // The gate is inert on a container-only composition: building the provider and resolving services does NOT
        // fire ValidateOnStart (that runs only from IHost.StartAsync — never called here). This is the inertness
        // 7qooby closes, and the load-bearing premise: if ValidateStartupGates were a no-op the SAFETY assertion
        // below would not throw, so the lock is non-vacuous.
        using var provider = BuildProvider(allowVolatile: false);
        _ = provider.GetService<IValidateOptions<GateOptions>>().ShouldNotBeNull();

        // SAFETY — the explicit host-less trigger fires the gate; a volatile store without opt-in fails startup.
        var thrown = Should.Throw<OptionsValidationException>(() => provider.ValidateStartupGates());
        thrown.Message.ShouldContain("volatile store configured without an explicit opt-in.");
    }

    [Fact]
    public void Let_a_valid_configuration_pass_and_return_the_provider_for_chaining()
    {
        // LIVENESS — a store that opted in (or is durable) passes clean; the trigger must not be inert-by-throwing.
        // Also returns the same provider so it chains after BuildServiceProvider().
        using var provider = BuildProvider(allowVolatile: true);

        var returned = provider.ValidateStartupGates();

        returned.ShouldBeSameAs(provider);
    }

    [Fact]
    public void No_op_cleanly_when_no_startup_validation_is_registered()
    {
        // LIVENESS — a container with no ValidateOnStart() gate has no IStartupValidator; the trigger no-ops rather
        // than throwing, so calling it unconditionally after Build is always safe.
        using var provider = BuildProvider(allowVolatile: false, registerGate: false);

        Should.NotThrow(() => provider.ValidateStartupGates());
    }

    [Fact]
    public void Reject_a_null_provider()
    {
        _ = Should.Throw<ArgumentNullException>(
            static () => ((IServiceProvider)null!).ValidateStartupGates());
    }
}
