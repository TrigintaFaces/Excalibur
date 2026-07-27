// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a host-less trigger for the fail-fast startup gates registered with
/// <see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}(OptionsBuilder{TOptions})"/>.
/// </summary>
public static class StartupValidationServiceProviderExtensions
{
    /// <summary>
    /// Runs every <c>ValidateOnStart()</c> gate registered in the container immediately, for consumers who
    /// build an <see cref="IServiceProvider"/> and resolve services directly without ever starting a host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ValidateOnStart()</c> gates (for example the audit-store, key, grant, schedule, and separation-of-duties
    /// durability checks) fire from the host's startup validation, which only runs when the application calls
    /// <c>IHost.StartAsync</c>. A consumer who composes the container manually — a custom serverless runtime, a
    /// manual <see cref="IServiceProvider"/>, a unit of work that never builds a host — never triggers them, so
    /// the fail-fast guarantees are silently inert. Such a consumer MUST call this method once, immediately after
    /// building the provider, to run the same checks the host would have run at start.
    /// </para>
    /// <para>
    /// This runs the framework's own <see cref="IStartupValidator"/> (what <c>ValidateOnStart()</c> registers), so
    /// it validates <em>every</em> registered gate — a gate added later is covered automatically, with no list to
    /// keep in sync. It no-ops when nothing registered startup validation, and is safe to call once after build.
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
    /// <exception cref="OptionsValidationException">A registered startup gate fails validation.</exception>
    public static IServiceProvider ValidateStartupGates(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        provider.GetService<IStartupValidator>()?.Validate();

        return provider;
    }
}
