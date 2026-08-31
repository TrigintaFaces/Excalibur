// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with ASP.NET Core hosting (request-scope-aware
/// handler resolution) and observability into a single registration call for web applications.
/// </summary>
public static class DispatchAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Excalibur.Dispatch for an ASP.NET Core application: the core dispatcher, OpenTelemetry
    /// observability, and the ambient-scope integration so scoped message handlers resolve from — and
    /// share state with — the active request scope.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDispatch">Optional additional dispatch builder configuration. Supplying it takes over handler registration: when it is omitted, handlers are discovered by scanning the entry assembly; when it is supplied, only the handlers it names are registered.</param>
    /// <returns>The service collection for chaining.</returns>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
    public static IServiceCollection AddDispatchAspNetCore(
        this IServiceCollection services,
        Action<IDispatchBuilder>? configureDispatch = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddDispatch(dispatch =>
            {
                dispatch.UseObservability();
                // The consumer supplied no configuration of their own, so nothing has named a handler and
                // nothing will. Discover them from the entry assembly, which is what this call did before the
                // lambda was synthesised. A consumer who DOES supply a lambda owns handler registration.
                if (configureDispatch is null)
                {
                    _ = dispatch.AddHandlersFromEntryAssembly();
                }
                else
                {
                    configureDispatch(dispatch);
                }
            })
            .AddDispatchAmbientScope();
    }
}
