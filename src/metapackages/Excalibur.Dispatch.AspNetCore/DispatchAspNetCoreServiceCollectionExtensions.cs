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
    /// <param name="configureDispatch">Optional additional dispatch builder configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatchAspNetCore(
        this IServiceCollection services,
        Action<IDispatchBuilder>? configureDispatch = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddDispatch(dispatch =>
            {
                dispatch.UseObservability();
                configureDispatch?.Invoke(dispatch);
            })
            .AddDispatchAmbientScope();
    }
}
