// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a host-less trigger for the framework's fail-fast startup gates — both the options gates registered
/// with <see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}(OptionsBuilder{TOptions})"/> and the
/// prerequisite wiring checks registered as <see cref="IStartupPrerequisiteValidator"/>.
/// </summary>
public static class StartupValidationServiceProviderExtensions
{
    /// <summary>
    /// Runs the container's startup gates immediately, for consumers who build an <see cref="IServiceProvider"/>
    /// and resolve services directly without ever starting a host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both families of gate fire from host startup, which only happens when the application calls
    /// <c>IHost.StartAsync</c>: the <c>ValidateOnStart()</c> options gates (for example the audit-store, key,
    /// grant, schedule, and separation-of-duties durability checks) and the prerequisite wiring checks (for
    /// example "event sourcing was added but no <c>IEventStore</c> provider was selected"). A consumer who
    /// composes the container manually — a custom serverless runtime, a manual <see cref="IServiceProvider"/>, a
    /// unit of work that never builds a host — never triggers them, so the fail-fast guarantees are silently
    /// inert. Such a consumer MUST call this method once, immediately after building the provider.
    /// </para>
    /// <para>
    /// It runs the framework's own <see cref="IStartupValidator"/> (what <c>ValidateOnStart()</c> registers), so
    /// every registered options gate is covered automatically with no list to keep in sync, and then every
    /// <see cref="IStartupPrerequisiteValidator"/> in the container. It starts no hosted services: outbox
    /// processors, leader election, and other background work stay unstarted, because a container the consumer
    /// never intended to run as a host must not acquire one by asking whether it is wired correctly. It no-ops
    /// when neither family is registered, and is safe to call once after build.
    /// </para>
    /// <para>
    /// One class of check is deliberately not covered: a gate that must perform I/O to decide — probing a remote
    /// secret mount, reading a physical table schema — cannot run from a synchronous method without blocking.
    /// Those remain hosted-only and each carries its own fail-closed check on the path it protects, so a
    /// host-less consumer fails on first use of that path rather than silently proceeding.
    /// </para>
    /// <para>
    /// Hosts that build an <see cref="IServiceProvider"/> through the generic host and call <c>StartAsync</c> —
    /// including Azure Functions and AWS Lambda on the isolated-worker model — already run these gates at start
    /// and do not need this call. It is for the genuinely host-less path only.
    /// </para>
    /// </remarks>
    /// <param name="provider">The built service provider whose startup gates should run now.</param>
    /// <returns>The same <paramref name="provider"/>, so the call can be chained after <c>BuildServiceProvider()</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="OptionsValidationException">A registered options gate fails validation.</exception>
    /// <exception cref="InvalidOperationException">A registered prerequisite is missing or misconfigured.</exception>
    public static IServiceProvider ValidateStartupGates(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        provider.GetService<IStartupValidator>()?.Validate();

        foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
        {
            validator.Validate();
        }

        return provider;
    }
}
