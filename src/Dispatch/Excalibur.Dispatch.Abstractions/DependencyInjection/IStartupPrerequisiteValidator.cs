// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A fail-fast wiring check that must run before the first message is handled, and that can run against a
/// built container alone — without starting a host.
/// </summary>
/// <remarks>
/// <para>
/// Prerequisite validators answer one question: did the consumer wire everything this feature requires? A
/// missing store provider, an encryption feature switched on with no key provider, an aggregate registered
/// with no serializable event type. They inspect the container and throw with actionable guidance; they
/// perform no I/O and have no side effects, so running one twice is indistinguishable from running it once.
/// </para>
/// <para>
/// Implementations are normally also registered as an <c>IHostedService</c>, which is what places the check
/// in a host's startup pipeline. Implementing this interface as well is what lets the same check run for a
/// consumer who builds an <see cref="IServiceProvider"/> and never starts a host — see
/// <see cref="StartupValidationServiceProviderExtensions.ValidateStartupGates(IServiceProvider)"/>. Register
/// both, so both topologies fail fast:
/// </para>
/// <code>
/// services.TryAddEnumerable(ServiceDescriptor.Singleton&lt;IHostedService, MyPrerequisiteValidator&gt;());
/// services.TryAddEnumerable(ServiceDescriptor.Singleton&lt;IStartupPrerequisiteValidator, MyPrerequisiteValidator&gt;());
/// </code>
/// <para>
/// A check that needs I/O — probing a remote mount, reading a physical schema — is deliberately outside this
/// contract, because it cannot be performed from a synchronous method without blocking. Such a check stays an
/// <c>IHostedService</c> and carries its own fail-closed floor on the path it protects.
/// </para>
/// </remarks>
public interface IStartupPrerequisiteValidator
{
    /// <summary>
    /// Verifies the prerequisites this validator guards, throwing when they are not satisfied.
    /// </summary>
    /// <remarks>
    /// Must be free of I/O and side effects, and safe to call more than once on the same container.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A required prerequisite is missing or misconfigured.</exception>
    void Validate();
}
